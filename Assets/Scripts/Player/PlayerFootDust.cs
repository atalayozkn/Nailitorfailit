using MoreMountains.Feedbacks;
using UnityEngine;

// Dust puffs under the feet on walk/run steps and a radial burst on landing, plus a light Feel camera shake on landing.
// Opaque unlit blobs that shrink away (material and shape set by Tools/NailIt/Feel/4). Each puff kind sets its
// own count, size, speed and brightness per particle (EmitParams), so the particle system's start values do not matter. Everything is skipped while the player is ragdolled (stunned) or dead.
public class PlayerFootDust : MonoBehaviour
{
    [System.Serializable]
    public struct PuffSettings
    {
        [Min(0)] public int count;
        [Tooltip("Start size range in metres.")]
        public Vector2 size;
        [Tooltip("Outward speed range in m/s.")]
        public Vector2 outwardSpeed;
        [Tooltip("Upward speed range in m/s.")]
        public Vector2 upSpeed;
        [Tooltip("Lifetime range in seconds.")]
        public Vector2 lifetime;
        [Tooltip("Radius of the ring the puffs spawn on.")]
        public float spawnRadius;
        [Tooltip("Random brightness range per blob, so overlapping blobs stay readable.")]
        public Vector2 brightness;
        [Tooltip("0 = evenly around the feet, 1 = all kicked out behind the player.")]
        [Range(0f, 1f)] public float backwardBias;

        public PuffSettings(int count, Vector2 size, Vector2 outwardSpeed, Vector2 upSpeed, Vector2 lifetime,
            float spawnRadius, Vector2 brightness, float backwardBias)
        {
            this.count = count;
            this.size = size;
            this.outwardSpeed = outwardSpeed;
            this.upSpeed = upSpeed;
            this.lifetime = lifetime;
            this.spawnRadius = spawnRadius;
            this.brightness = brightness;
            this.backwardBias = backwardBias;
        }
    }

    [Header("References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private ParticleSystem dust;
    [Tooltip("Feel player with a Cinemachine Impulse, played on landing.")]
    [SerializeField] private MMF_Player landFeedback;

    [Header("Look")]
    [Tooltip("Dust tint. Kept mid-tone so it reads on both light and dark floors.")]
    [SerializeField] private Color dustColor = new Color(0.62f, 0.55f, 0.45f, 1f);
    [SerializeField] private float footHeight = 0.08f;

    [Header("Puffs")]
    [Tooltip("Small but visible puff on each walking step.")]
    [SerializeField] private PuffSettings walk = new PuffSettings(3,
        new Vector2(0.11f, 0.17f), new Vector2(0.3f, 0.7f), new Vector2(0.15f, 0.35f),
        new Vector2(0.45f, 0.7f), 0.12f, new Vector2(0.85f, 1.1f), 0.5f);
    [Tooltip("Bigger, denser puff kicked out behind on each running step.")]
    [SerializeField] private PuffSettings run = new PuffSettings(7,
        new Vector2(0.15f, 0.25f), new Vector2(0.6f, 1.3f), new Vector2(0.25f, 0.5f),
        new Vector2(0.55f, 0.85f), 0.15f, new Vector2(0.85f, 1.1f), 0.7f);
    [Tooltip("Radial ring burst on landing.")]
    [SerializeField] private PuffSettings land = new PuffSettings(18,
        new Vector2(0.175f, 0.3f), new Vector2(1.8f, 2.8f), new Vector2(0.1f, 0.3f),
        new Vector2(0.6f, 0.95f), 0.25f, new Vector2(0.85f, 1.1f), 0f);

    private void Awake()
    {
        if (movement == null) movement = GetComponentInParent<PlayerMovement>();
        if (stateMachine == null) stateMachine = GetComponentInParent<PlayerStateMachine>();
    }

    private void OnEnable()
    {
        if (movement == null) return;
        movement.Stepped += HandleStep;
        movement.Landed += HandleLand;
    }

    private void OnDisable()
    {
        if (movement == null) return;
        movement.Stepped -= HandleStep;
        movement.Landed -= HandleLand;
    }

    private void Update()
    {
        // Ragdoll: kill any running shake at once.
        if (!IsRagdolled()) return;
        if (landFeedback != null && landFeedback.IsPlaying) landFeedback.StopFeedbacks();
    }

    private void HandleStep(bool onWater)
    {
        if (onWater || IsRagdolled()) return;
        Emit(movement.IsRunning() ? run : walk);
    }

    private void HandleLand()
    {
        if (IsRagdolled()) return;
        Emit(land);
        if (landFeedback != null) landFeedback.PlayFeedbacks();
    }

    private void Emit(in PuffSettings puff)
    {
        if (dust == null || puff.count <= 0) return;

        Transform body = movement.transform;
        Vector3 origin = body.position + Vector3.up * footHeight;
        Vector3 back = -body.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
        back.Normalize();

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };

        for (int i = 0; i < puff.count; i++)
        {
            // Landing: evenly spaced ring with jitter. Steps: random, leaning behind the player.
            float angle = puff.backwardBias <= 0f
                ? (i + Random.Range(-0.3f, 0.3f)) / puff.count * Mathf.PI * 2f
                : Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (puff.backwardBias > 0f)
            {
                dir = Vector3.Slerp(dir, back, puff.backwardBias * Random.Range(0.5f, 1f));
                dir.y = 0f;
                dir.Normalize();
            }

            p.position = origin + dir * puff.spawnRadius;
            p.velocity = dir * Random.Range(puff.outwardSpeed.x, puff.outwardSpeed.y)
                         + Vector3.up * Random.Range(puff.upSpeed.x, puff.upSpeed.y);
            p.startSize = Random.Range(puff.size.x, puff.size.y);
            p.startLifetime = Random.Range(puff.lifetime.x, puff.lifetime.y);
            Color c = dustColor * Random.Range(puff.brightness.x, puff.brightness.y);
            c.a = 1f;
            p.startColor = c;
            p.rotation = Random.Range(0f, 360f);
            dust.Emit(p, 1);
        }
    }

    private bool IsRagdolled()
    {
        if (stateMachine == null) return false;
        if (stateMachine.isDead) return true;

        PlayerStates state = stateMachine.currentPlayerState;
        return state == PlayerStates.Stunned || state == PlayerStates.Dead;
    }
}
