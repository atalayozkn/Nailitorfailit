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

    private Car_Active carActive;
    private Rigidbody body;

    private void Awake()
    {
        carActive = GetComponent<Car_Active>();
        body = GetComponent<Rigidbody>();
    }

    public override void OnStartServer()
    {
        if (carActive != null) carActive.enabled = true;

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

    [Server]
    private void ServerDespawn()
    {
        if (logActions) InteractionDebug.Log($"[MP-CAR] despawn '{name}'");
        NetworkServer.Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other) => ServerTryHit(other);

    private void OnCollisionEnter(Collision collision) => ServerTryHit(collision.collider);

    private void ServerTryHit(Collider other)
    {
        if (!isServer || other == null) return;
        if ((whatIsPlayer.value & (1 << other.gameObject.layer)) == 0) return;

        PlayerScripts.PlayerNetwork_MP player = other.GetComponentInParent<PlayerScripts.PlayerNetwork_MP>();
        if (player == null || player.isDead) return;

        player.RequestDeath();

        if (logActions)
            InteractionDebug.Log($"[MP-CAR] '{name}' hit '{player.name}' - death requested on the server.");
    }
}
