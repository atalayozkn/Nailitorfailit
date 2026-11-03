using System.Reflection;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MpCarHitSync_MP : NetworkBehaviour
{
    [Tooltip("Which layers count as a player hit. Copied from Car_Active when the prefab is generated.")]
    [SerializeField] private LayerMask whatIsPlayer;

    [Tooltip("Server-side despawn delay. 0 = never despawn (Car_Active lifetime decides).")]
    [SerializeField] private float serverLifeTime = 10f;

    [Tooltip("Log every hit and despawn.")]
    [SerializeField] private bool logActions = true;

    [Tooltip("Server-side overlap distance that counts as a hit, even if no physics contact is reported.")]
    [SerializeField] private float hitRadius = 1.25f;

    private static readonly FieldInfo MaxLifeTimeField =
        typeof(Car_Active).GetField("maxLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);

    private float nextMissLogAt;
    private float nextProximityCheck;

    private Car_Active carActive;
    private Rigidbody body;

    private float nextSampleAt;
    private Vector3 lastSamplePosition;

    private void Awake()
    {
        carActive = GetComponent<Car_Active>();
        body = GetComponent<Rigidbody>();
    }

    public override void OnStartServer()
    {
        if (carActive != null)
        {
            carActive.enabled = true;

            if (MaxLifeTimeField != null)
            {
                MaxLifeTimeField.SetValue(carActive, 1e9f);

                if (logActions)
                    InteractionDebug.Log("[MP-CAR] SP lifetime disabled on the server car " +
                                         "(despawn is networked now).");
            }
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

        NetworkIdentity identity = GetComponent<NetworkIdentity>();

        InteractionDebug.Log($"[MP-CAR] client received '{name}' netId={identity?.netId} " +
                             $"pos={transform.position}");

        if (body != null)
        {
            body.isKinematic = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        nextSampleAt = Time.unscaledTime + 1f;
        lastSamplePosition = transform.position;

        InteractionDebug.Log($"[MP-CAR] client received car netId={netId} " +
                             $"pos={transform.position} hasTransform={GetComponent<Mirror.NetworkTransformReliable>() != null}");
    }

    private void Update()
    {
        if (isServer)
        {
            if (Time.unscaledTime < nextProximityCheck) return;

            nextProximityCheck = Time.unscaledTime + 0.1f;
            ServerCheckProximity();
            return;
        }

        if (Time.unscaledTime < nextSampleAt) return;

        nextSampleAt = Time.unscaledTime + 1f;

        float moved = Vector3.Distance(transform.position, lastSamplePosition);
        lastSamplePosition = transform.position;

        InteractionDebug.Log($"[MP-CAR] client car netId={netId} moved={moved:F2}m/s " +
                             $"pos={transform.position}");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        CancelInvoke(nameof(ServerDespawn));
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

            InteractionDebug.Log($"[MP-CAR-HIT] '{name}' proximity hit on '{player.name}' " +
                                 $"(distance {Vector3.Distance(closest, center):0.00}) - death requested.");

            player.RequestDeath();
            KeepAliveAfterHit();
            return;
        }
    }

    private void KeepAliveAfterHit()
    {
        CancelInvoke(nameof(ServerDespawn));
        Invoke(nameof(ServerDespawn), 4f);
    }

    private void OnTriggerEnter(Collider other) => ServerTryHit(other);

    private void OnCollisionEnter(Collision collision) => ServerTryHit(collision.collider);

    private void ServerTryHit(Collider other)
    {
        if (!isServer || other == null) return;

        PlayerScripts.PlayerNetwork_MP player = other.GetComponentInParent<PlayerScripts.PlayerNetwork_MP>();

        if (player == null)
        {
            if (logActions && Time.unscaledTime >= nextMissLogAt)
            {
                nextMissLogAt = Time.unscaledTime + 3f;
                InteractionDebug.Log($"[MP-CAR-HIT] '{name}' touched '{other.name}' " +
                                     $"layer={other.gameObject.layer} - not a player, ignored.");
            }

            return;
        }

        if (player.isDead) return;

        InteractionDebug.Log($"[MP-CAR-HIT] '{name}' -> '{player.name}' (layer {other.gameObject.layer}, " +
                             $"mask {whatIsPlayer.value}) death requested.");

        player.RequestDeath();
        KeepAliveAfterHit();
    }
}
