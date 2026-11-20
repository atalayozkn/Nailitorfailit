using UnityEngine;

// Flickers a fire's point light. With a flash it starts brighter and fades out (ignition burst).
[RequireComponent(typeof(Light))]
public class FireLightFlicker : MonoBehaviour
{
    [SerializeField] private float baseIntensity = 5f;
    [SerializeField] private float flash;
    [SerializeField] private float flashDuration = 0.8f;
    [SerializeField] private float speed = 9f;

    private Light lightSource;
    private float seed;
    private float age;

    public void Init(float intensity, float flashAmount)
    {
        baseIntensity = intensity;
        flash = flashAmount;
    }

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        seed = Random.value * 100f;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Time.time * speed + seed;
        float flicker = 0.75f + 0.35f * Mathf.PerlinNoise(t, seed) + 0.1f * Mathf.Sin(t * 2.3f);
        float intensity = baseIntensity * flicker;

        if (flash > 0f)
        {
            float k = Mathf.Clamp01(age / flashDuration);
            intensity *= Mathf.Lerp(flash, 0f, k * k);
        }
        else
        {
            // Fade in so a new fire does not pop.
            intensity *= Mathf.Clamp01(age * 3f);
        }
        lightSource.intensity = intensity;
    }
}
