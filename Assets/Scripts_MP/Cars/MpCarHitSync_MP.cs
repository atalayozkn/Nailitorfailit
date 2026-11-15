using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

// Networked version of Car_Active's hit logic, matching SP:
//   body collision  -> Bump  (player stunned / ragdoll for 3 s)
//   crash trigger   -> Crash (player dies, the mock player shows on the car, camera tracks the car)
// Remote players are kinematic copies on the server, so a car would stop dead against them (it cannot push a
// kinematic body: speed 0 until despawn). The server therefore lets the car drive THROUGH remote players
// (Physics.IgnoreCollision) and detects the hit itself from the car's own colliders (front trigger = crash,
// solid body = bump), every physics step.
// The server decides, every machine plays the same result. Car_Active's own Bump/Crash are switched off
// (its player mask is cleared) because Unity still delivers collision callbacks to disabled behaviours,
// which used to trigger unsynced local deaths on clients and the mock player only on the host.
[RequireComponent(typeof(Rigidbody))]
public class MpCarHitSync_MP : NetworkBehaviour
{
    [Tooltip("Which layers count as a player hit. Copied from Car_Active when the prefab is generated.")]
    [SerializeField] private LayerMask whatIsPlayer;

    [Tooltip("Server-side despawn delay. 0 = never despawn (Car_Active lifetime decides).")]
    [SerializeField] private float serverLifeTime = 10f;

    [Tooltip("Log every hit and despawn.")]
    [SerializeField] private bool logActions = true;

    [Tooltip("Players closer than this (metres) are checked for contact and ignored by the car's physics.")]
    [SerializeField] private float contactRange = 8f;

    [Tooltip("Seconds a player is immune after any car hit (stops one hit firing 3-4 reactions).")]
    [SerializeField] private float perPlayerHitCooldown = 2f;

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo MaxLifeTimeField = typeof(Car_Active).GetField("maxLifeTime", Private);
    private static readonly FieldInfo PlayerMaskField = typeof(Car_Active).GetField("whatIsPlayer", Private);
    private static readonly FieldInfo MockPlayerField = typeof(Car_Active).GetField("mockPlayerObject", Private);
    private static readonly FieldInfo CrashEventField = typeof(Car_Active).GetField("onCrashEvent", Private);

    // Shared by all cars: one player can only be hit once per cooldown, whichever car hits.
    private static readonly Dictionary<uint, double> lastHitAt = new Dictionary<uint, double>();

    private Car_Active carActive;
    private Rigidbody body;
    private bool crashed;

    // Road end (server): the car is removed when it has driven past this point.
    private bool hasRoute;
    private Vector3 routeEnd;
    private Vector3 routeDirection;

    public void SetRoute(Vector3 end, Vector3 direction)
    {
        hasRoute = true;
        routeEnd = end;
        routeDirection = direction;
    }

    private Collider hitTrigger; // front zone: SP Car_Active.OnTriggerEnter -> Crash
    private Collider hitBody;    // solid body: SP Car_Active.OnCollisionEnter -> Bump
    private static PlayerScripts.PlayerNetwork_MP[] cachedPlayers = new PlayerScripts.PlayerNetwork_MP[0];
    private static Collider[][] cachedPlayerColliders = new Collider[0][];
    private static float cachedPlayersAt = -1f;

    private void Awake()
    {
        carActive = GetComponent<Car_Active>();
        body = GetComponent<Rigidbody>();

        if (carActive != null && PlayerMaskField != null) PlayerMaskField.SetValue(carActive, (LayerMask)0);

        foreach (Collider candidate in GetComponents<Collider>())
        {
            if (candidate.isTrigger) { if (hitTrigger == null) hitTrigger = candidate; }
            else if (hitBody == null) hitBody = candidate;
        }
    }

    public override void OnStartServer()
    {
        if (carActive != null)
        {
            carActive.enabled = true;
            if (MaxLifeTimeField != null) MaxLifeTimeField.SetValue(carActive, 1e9f);
        }

        if (body != null)
        {
            body.isKinematic = false;
            body.WakeUp();
        }

        // With a road end the car is removed there; the timer is only a safety net for a car stuck somewhere.
        float lifetime = hasRoute ? Mathf.Max(serverLifeTime, 60f) : serverLifeTime;
        if (lifetime > 0f) Invoke(nameof(ServerDespawn), lifetime);
    }

    public override void OnStartClient()
    {
        if (isServer) return;

        if (carActive != null) carActive.enabled = false;

        if (body != null)
        {
            // Velocities can only be set on a dynamic body: zero them first, then make it kinematic.
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        CancelInvoke(nameof(ServerDespawn));
    }

    // Every physics step: the car covers ~0.35 m per step at full speed, less than the front zone is deep.
    [ServerCallback]
    private void FixedUpdate()
    {
        if (hasRoute && Vector3.Dot(transform.position - routeEnd, routeDirection) >= 0f)
        {
            ServerDespawn();
            return;
        }

        if (!crashed) ServerCheckContacts();
    }

    private void ServerDespawn()
    {
        if (!NetworkServer.active)
        {
            Destroy(gameObject);
            return;
        }

        if (logActions) InteractionDebug.Log($"[MP-CAR] despawn '{name}'");
        NetworkServer.Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isServer) ServerHit(other, crash: true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isServer) ServerHit(collision.collider, crash: false);
    }

    private static PlayerScripts.PlayerNetwork_MP[] Players()
    {
        if (Time.unscaledTime - cachedPlayersAt > 0.5f)
        {
            cachedPlayersAt = Time.unscaledTime;
            cachedPlayers = FindObjectsByType<PlayerScripts.PlayerNetwork_MP>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            cachedPlayerColliders = new Collider[cachedPlayers.Length][];

            for (int i = 0; i < cachedPlayers.Length; i++)
                cachedPlayerColliders[i] = cachedPlayers[i].GetComponentsInChildren<Collider>(true);
        }

        return cachedPlayers;
    }

    [Server]
    private void ServerCheckContacts()
    {
        float rangeSqr = contactRange * contactRange;
        PlayerScripts.PlayerNetwork_MP[] players = Players();

        for (int i = 0; i < players.Length; i++)
        {
            PlayerScripts.PlayerNetwork_MP player = players[i];
            if (player == null || player.isDead) continue;

            Collider capsule = player.GetComponent<Collider>(); // the player's root body collider
            if (capsule == null || !capsule.enabled) continue;
            if ((capsule.bounds.center - transform.position).sqrMagnitude > rangeSqr) continue;

            // The car must keep its speed and direction when it hits a player (a car has mass 1 here: even a
            // ragdoll limb deflects it). It drives through every collider of the player - body, ragdoll limbs, the
            // active ragdoll too. Re-applied every step: enabling a collider (ragdoll at stun) resets its ignore.
            Collider[] colliders = cachedPlayerColliders[i];
            foreach (Collider c in colliders)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;

                if (hitBody != null) Physics.IgnoreCollision(hitBody, c, true);
                if (hitTrigger != null) Physics.IgnoreCollision(hitTrigger, c, true);
            }

            if (Overlaps(hitTrigger, capsule))
            {
                ServerApply(player, crash: true, "front-zone");
                return;
            }

            if (Overlaps(hitBody, capsule)) ServerApply(player, crash: false, "body");
        }
    }

    private static bool Overlaps(Collider car, Collider other)
    {
        if (car == null || other == null || !car.enabled) return false;

        return Physics.ComputePenetration(car, car.transform.position, car.transform.rotation,
                                          other, other.transform.position, other.transform.rotation,
                                          out _, out _);
    }

    [Server]
    private void ServerHit(Collider other, bool crash)
    {
        if (other == null || crashed) return;

        PlayerScripts.PlayerNetwork_MP player = other.GetComponentInParent<PlayerScripts.PlayerNetwork_MP>();
        if (player == null || player.isDead) return;

        ServerApply(player, crash, crash ? "trigger" : "collision");
    }

    [Server]
    private void ServerApply(PlayerScripts.PlayerNetwork_MP player, bool crash, string source)
    {
        uint id = player.netId;
        double now = NetworkTime.time;

        // NetworkTime restarts with every host session while this dictionary lives on: a stale (larger) entry made
        // now - last negative and the car could never hit that netId again.
        if (lastHitAt.TryGetValue(id, out double last) && now >= last && now - last < perPlayerHitCooldown) return;
        lastHitAt[id] = now;

        if (logActions)
            InteractionDebug.Log($"[MP-CAR-HIT] '{name}' {(crash ? "CRASH" : "bump")} on '{player.name}' ({source}).");

        if (!crash)
        {
            player.ServerStun();
            return;
        }

        crashed = true;
        RpcCrash(player.netIdentity);
        player.ServerKill(DeathReason.Car);

        // With a road end the car keeps driving (the victim rides on it) and is removed at the end of the road.
        if (!hasRoute)
        {
            CancelInvoke(nameof(ServerDespawn));
            Invoke(nameof(ServerDespawn), 4f);
        }
    }

    // Same visuals as Car_Active.Crash, on every machine.
    [ClientRpc]
    private void RpcCrash(NetworkIdentity victim)
    {
        if (carActive != null)
        {
            if (MockPlayerField?.GetValue(carActive) is GameObject mock && mock != null && !ShowVictimStandIn(victim, mock))
                mock.SetActive(true);

            if (CrashEventField?.GetValue(carActive) is UnityEvent onCrash) onCrash.Invoke();
        }

        if (victim == null) return;

        PlayerStateMachine stateMachine = victim.GetComponentInChildren<PlayerStateMachine>(true);
        if (stateMachine != null) stateMachine.SetTrackTarget(transform);
    }

    private static AnimationClip FindClip(RuntimeAnimatorController controller, string clipName)
    {
        if (controller == null) return null;

        foreach (AnimationClip clip in controller.animationClips)
            if (clip != null && clip.name == clipName) return clip;

        return null;
    }

    // The prefab's stand-in ("mock player") is an old, untextured model: the victim looked undressed on the car.
    // Use a copy of the victim's own model instead (builder outfit, player colour, the pose it had when hit).
    private bool ShowVictimStandIn(NetworkIdentity victim, GameObject mock)
    {
        if (victim == null) return false;

        PlayerStateMachine stateMachine = victim.GetComponentInChildren<PlayerStateMachine>(true);
        Animator animator = stateMachine != null ? stateMachine.animator : null;
        if (animator == null || animator.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) return false;

        // Instantiated under an inactive holder so none of the copied scripts run before they are removed.
        var holder = new GameObject("VictimStandInHolder");
        holder.SetActive(false);

        GameObject copy = Instantiate(animator.gameObject, holder.transform);
        copy.name = "VictimStandIn";

        foreach (Joint joint in copy.GetComponentsInChildren<Joint>(true)) DestroyImmediate(joint);
        foreach (Rigidbody rb in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
        foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(script);
        foreach (Animator copiedAnimator in copy.GetComponentsInChildren<Animator>(true)) DestroyImmediate(copiedAnimator);

        // Removing the Animator resets the bones to the bind pose (T-pose). The controller has no "hit by a car"
        // clip, so pose the copy with the airborne frame of the Jump clip: arms out, legs bent, thrown onto the car.
        AnimationClip hitPose = FindClip(animator.runtimeAnimatorController, "Jump");
        if (hitPose != null) hitPose.SampleAnimation(copy, Mathf.Min(0.3f, hitPose.length));

        copy.transform.SetParent(mock.transform.parent, true);
        copy.transform.SetPositionAndRotation(mock.transform.position, mock.transform.rotation);
        copy.SetActive(true);

        Destroy(holder);
        mock.SetActive(false);
        return true;
    }
}
