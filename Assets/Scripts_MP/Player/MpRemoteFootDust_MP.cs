using Mirror;
using UnityEngine;

// Walking dust for the OTHER players. The owner raises PlayerMovement.Stepped from real input; remote copies only move
// along the network, so their steps are derived from how fast they travel on the ground.
[RequireComponent(typeof(PlayerFootDust))]
public class MpRemoteFootDust_MP : MonoBehaviour
{
    [SerializeField] private float minSpeed = 0.8f;
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float walkStep = 0.5f;
    [SerializeField] private float runStep = 0.25f;

    private PlayerFootDust dust;
    private PlayerMovement movement;
    private NetworkIdentity identity;
    private Vector3 lastPosition;
    private float stepTimer;

    private void Awake()
    {
        dust = GetComponent<PlayerFootDust>();
        movement = GetComponentInParent<PlayerMovement>();
        identity = GetComponentInParent<NetworkIdentity>();
        lastPosition = transform.position;
    }

    private void OnEnable() => lastPosition = transform.position;

    private void Update()
    {
        Vector3 position = transform.position;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = position - lastPosition;
        lastPosition = position;

        if (identity == null || identity.isOwned) return; // the owner already gets Stepped from PlayerMovement

        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        float speed = flat.magnitude / dt;
        bool onGround = Mathf.Abs(delta.y) / dt < 1.5f;

        if (speed < minSpeed || !onGround)
        {
            stepTimer = 0f;
            return;
        }

        bool running = speed >= runSpeed;
        stepTimer += dt;
        if (stepTimer < (running ? runStep : walkStep)) return;

        stepTimer = 0f;
        dust.PlayRemoteStep(running);
        if (movement != null) movement.PlayRemoteStep(false); // the footstep sound, positioned at that player
    }
}
