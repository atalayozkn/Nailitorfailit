using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

// Networked version of Car_Active's hit logic, matching SP:
//   body collision  -> Bump  (player stunned / ragdoll for 3 s)
//   crash trigger   -> Crash (player dies, the mock player shows on the car, camera tracks the car)
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

    [Tooltip("Server-side overlap distance that counts as a crash when no physics contact is reported.")]
    [SerializeField] private float hitRadius = 1.25f;

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
    private float nextProximityCheck;
    private bool crashed;

    private void Awake()
    {
        carActive = GetComponent<Car_Active>();
        body = GetComponent<Rigidbody>();

        if (carActive != null && PlayerMaskField != null) PlayerMaskField.SetValue(carActive, (LayerMask)0);
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

        if (serverLifeTime > 0f) Invoke(nameof(ServerDespawn), serverLifeTime);
    }

    public override void OnStartClient()
    {
        if (isServer) return;

        if (carActive != null) carActive.enabled = false;

        if (body != null)
        {
            body.isKinematic = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        CancelInvoke(nameof(ServerDespawn));
    }

    [ServerCallback]
    private void Update()
    {
        if (crashed || Time.unscaledTime < nextProximityCheck) return;

        nextProximityCheck = Time.unscaledTime + 0.1f;
        ServerCheckProximity();
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

    [Server]
    private void ServerCheckProximity()
    {
        Vector3 center = transform.position + Vector3.up * 0.6f;
        float radiusSqr = hitRadius * hitRadius;

        foreach (PlayerScripts.PlayerNetwork_MP player in
                 FindObjectsByType<PlayerScripts.PlayerNetwork_MP>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (player == null || player.isDead) continue;

            Collider playerCollider = player.GetComponentInChildren<Collider>(true);
            Vector3 closest = playerCollider != null ? playerCollider.bounds.ClosestPoint(center) : player.transform.position;

            if ((closest - center).sqrMagnitude > radiusSqr) continue;

            ServerApply(player, crash: true, "proximity");
            return;
        }
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

        if (lastHitAt.TryGetValue(id, out double last) && now - last < perPlayerHitCooldown) return;
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

        CancelInvoke(nameof(ServerDespawn));
        Invoke(nameof(ServerDespawn), 4f);
    }

    // Same visuals as Car_Active.Crash, on every machine.
    [ClientRpc]
    private void RpcCrash(NetworkIdentity victim)
    {
        if (carActive != null)
        {
            if (MockPlayerField?.GetValue(carActive) is GameObject mock && mock != null) mock.SetActive(true);
            if (CrashEventField?.GetValue(carActive) is UnityEvent onCrash) onCrash.Invoke();
        }

        if (victim == null) return;

        PlayerStateMachine stateMachine = victim.GetComponentInChildren<PlayerStateMachine>(true);
        if (stateMachine != null) stateMachine.SetTrackTarget(transform);
    }
}
