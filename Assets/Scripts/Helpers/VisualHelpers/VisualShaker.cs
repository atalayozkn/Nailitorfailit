using UnityEngine;

// Shakes a visual-only child (mesh holder) with smooth noise. Never put colliders under the target:
// the physics body and its colliders stay still, only what is drawn moves.
public class VisualShaker : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform that holds only renderers. Defaults to this transform.")]
    [SerializeField] private Transform target;

    [Header("Shake")]
    [SerializeField] private float positionAmplitude = 0.01f;
    [Tooltip("Degrees.")]
    [SerializeField] private float rotationAmplitude = 0.5f;
    [SerializeField] private float frequency = 20f;
    [SerializeField] private bool shakeOnEnable = true;
    [Tooltip("Seconds to fade the shake in or out.")]
    [SerializeField] private float fadeTime = 0.25f;

    [Header("Speed Scaling (optional)")]
    [Tooltip("When set, the shake scales with this body's speed.")]
    [SerializeField] private Rigidbody speedSource;
    [SerializeField] private float fullShakeSpeed = 8f;
    [Range(0f, 1f)]
    [SerializeField] private float minSpeedFactor = 0.35f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float weight;
    private float targetWeight;
    private float seed;
    private bool hasBase;

    public void SetShaking(bool shaking)
    {
        targetWeight = shaking ? 1f : 0f;
    }

    private void Awake()
    {
        if (target == null) target = transform;
        seed = Random.value * 100f;
        CacheBase();
        WarnIfTargetHasColliders();
    }

    private void OnEnable()
    {
        weight = 0f;
        targetWeight = shakeOnEnable ? 1f : 0f;
    }

    private void OnDisable()
    {
        Restore();
    }

    private void LateUpdate()
    {
        float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
        weight = Mathf.MoveTowards(weight, targetWeight, step);

        if (weight <= 0f)
        {
            Restore();
            return;
        }

        float strength = weight * SpeedFactor();
        float t = Time.time * frequency;

        Vector3 offset = new Vector3(Noise(t, 0f), Noise(t, 1f), Noise(t, 2f)) * positionAmplitude * strength;
        Vector3 angles = new Vector3(Noise(t, 3f), Noise(t, 4f), Noise(t, 5f)) * rotationAmplitude * strength;

        target.localPosition = basePosition + offset;
        target.localRotation = baseRotation * Quaternion.Euler(angles);
    }

    private float SpeedFactor()
    {
        if (speedSource == null || fullShakeSpeed <= 0f) return 1f;
        if (speedSource.isKinematic) return 1f;

        float speed = speedSource.linearVelocity.magnitude;
        if (speed < 0.05f) return 0f;
        return Mathf.Lerp(minSpeedFactor, 1f, Mathf.Clamp01(speed / fullShakeSpeed));
    }

    private float Noise(float t, float channel)
    {
        return Mathf.PerlinNoise(seed + channel * 13.7f, t) * 2f - 1f;
    }

    private void CacheBase()
    {
        if (target == null) return;
        basePosition = target.localPosition;
        baseRotation = target.localRotation;
        hasBase = true;
    }

    private void Restore()
    {
        if (!hasBase || target == null) return;
        target.localPosition = basePosition;
        target.localRotation = baseRotation;
    }

    private void WarnIfTargetHasColliders()
    {
        if (target == null) return;

        foreach (Collider col in target.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue;
            Debug.LogWarning($"VisualShaker on '{name}': '{col.name}' has a collider under the shaken target, " +
                             "move it out so physics is not affected.", col);
            return;
        }
    }
}
