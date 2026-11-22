using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Soft light flicker: pulses a Graphic's alpha and scale with smooth noise (e.g. a lamp glow).
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIFlicker : MonoBehaviour
{
    [SerializeField] private Vector2 alphaRange = new Vector2(0.55f, 0.9f);
    [SerializeField] private float noiseSpeed = 1.6f;
    [SerializeField] private float scalePulse = 0.04f;
    [Tooltip("Chance per second of a short dip, like a loose bulb.")]
    [SerializeField] private float dipChance = 0.08f;

    private Graphic graphic;
    private Vector3 baseScale;
    private float seed;
    private float dipTimer;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        baseScale = transform.localScale;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float n = Mathf.PerlinNoise(seed, Time.unscaledTime * noiseSpeed);

        if (dipTimer <= 0f && Random.value < dipChance * dt) dipTimer = Random.Range(0.06f, 0.18f);
        float dip = 1f;
        if (dipTimer > 0f) { dipTimer -= dt; dip = 0.45f; }

        Color c = graphic.color;
        c.a = Mathf.Lerp(alphaRange.x, alphaRange.y, n) * dip;
        graphic.color = c;
        transform.localScale = baseScale * (1f + (n - 0.5f) * 2f * scalePulse);
    }
}
