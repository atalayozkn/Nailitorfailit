using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Style test only: butterflies, circling birds, synthesized bird chirps and drifting cloud shadows.
// The builder places the butterfly and bird objects as children so they also show in the editor.
public class StyleTestAmbience : MonoBehaviour
{
    [SerializeField] private Transform butterflyRoot;
    [SerializeField] private Transform birdRoot;
    [SerializeField] private List<Vector3> flowerSpots = new();
    [SerializeField] private List<Vector3> chirpSpots = new();
    [SerializeField] private Vector3 birdCenter;
    [SerializeField] private Light sun;
    [SerializeField] private Vector2 cloudDrift = new Vector2(0.45f, 0.18f);
    [SerializeField, Range(0f, 1f)] private float chirpVolume = 0.35f;
    [Tooltip("Recorded or generated chirps. When empty, short phrases are synthesized at start.")]
    [SerializeField] private List<AudioClip> chirpClips = new();

    private struct Flyer
    {
        public Transform t;
        public Vector3 target, velocity;
        public float rest, seed;
    }

    private readonly List<Flyer> butterflies = new();
    private readonly List<Flyer> birds = new();
    private readonly List<AudioSource> chirpSources = new();
    private readonly List<AudioClip> chirps = new();
    private UniversalAdditionalLightData sunData;
    private float nextChirp;

    public void SetChirps(List<AudioClip> clips) => chirpClips = clips;

    public void SetData(Transform butterflies, Transform birds, List<Vector3> flowers, List<Vector3> trees, Vector3 center, Light light)
    {
        butterflyRoot = butterflies;
        birdRoot = birds;
        flowerSpots = flowers;
        chirpSpots = trees;
        birdCenter = center;
        sun = light;
    }

    private void Start()
    {
        foreach (Transform t in butterflyRoot)
            butterflies.Add(new Flyer { t = t, target = t.position, seed = Random.value * 100f, rest = Random.value * 2f });
        foreach (Transform t in birdRoot)
            birds.Add(new Flyer { t = t, seed = Random.Range(0f, Mathf.PI * 2f), rest = Random.Range(9f, 17f) });

        if (chirpClips.Count > 0) chirps.AddRange(chirpClips);
        else for (int i = 0; i < 6; i++) chirps.Add(MakeChirp(i));
        foreach (var spot in chirpSpots)
        {
            var go = new GameObject("Chirp");
            go.transform.SetParent(transform);
            go.transform.position = spot;
            var source = go.AddComponent<AudioSource>();
            Sfx.Setup(source, 0.85f);
            source.minDistance = 4f;
            source.maxDistance = 35f;
            chirpSources.Add(source);
        }
        nextChirp = Time.time + 1f;

        if (sun != null) sun.TryGetComponent(out sunData);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < butterflies.Count; i++) butterflies[i] = MoveButterfly(butterflies[i], dt);
        for (int i = 0; i < birds.Count; i++) birds[i] = MoveBird(birds[i]);

        if (chirpSources.Count > 0 && Time.time >= nextChirp)
        {
            var source = chirpSources[Random.Range(0, chirpSources.Count)];
            source.pitch = Random.Range(0.9f, 1.12f);
            source.PlayOneShot(chirps[Random.Range(0, chirps.Count)], chirpVolume * Random.Range(0.6f, 1f));
            nextChirp = Time.time + Random.Range(1.2f, 4.5f);
        }

        if (sunData != null) sunData.lightCookieOffset += cloudDrift * dt;
    }

    // Wanders between flower spots with a fluttery path, and pauses on arrival.
    private Flyer MoveButterfly(Flyer f, float dt)
    {
        var pos = f.t.position;
        if (f.rest > 0f)
        {
            f.rest -= dt;
            f.t.position = pos + Vector3.up * Mathf.Sin(Time.time * 3f + f.seed) * 0.002f;
            if (f.rest <= 0f && flowerSpots.Count > 0)
            {
                var spot = flowerSpots[Random.Range(0, flowerSpots.Count)];
                f.target = spot + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.3f, 1.1f), Random.Range(-1.2f, 1.2f));
            }
            return f;
        }

        float time = Time.time + f.seed;
        var wobble = new Vector3(Mathf.PerlinNoise(time * 0.9f, 0f) - 0.5f, Mathf.PerlinNoise(0f, time * 1.3f) - 0.5f, Mathf.PerlinNoise(time * 0.7f, 5f) - 0.5f) * 3f;
        var desired = (f.target - pos).normalized * 1.1f + wobble;
        f.velocity = Vector3.Lerp(f.velocity, desired, dt * 2.5f);
        pos += f.velocity * dt;
        f.t.position = pos;
        var flat = new Vector3(f.velocity.x, 0f, f.velocity.z);
        if (flat.sqrMagnitude > 0.01f) f.t.rotation = Quaternion.Slerp(f.t.rotation, Quaternion.LookRotation(flat), dt * 6f);
        if ((f.target - pos).sqrMagnitude < 0.09f) f.rest = Random.Range(0.8f, 3f);
        return f;
    }

    // Big lazy loops overhead, banking into the turn.
    private Flyer MoveBird(Flyer f)
    {
        float angle = Time.time * (0.18f + f.rest * 0.004f) + f.seed;
        float radius = f.rest;
        var offset = new Vector3(Mathf.Cos(angle) * radius, 7.5f + Mathf.Sin(angle * 2.3f + f.seed) * 0.8f, Mathf.Sin(angle) * radius * 0.7f);
        var pos = birdCenter + offset;
        var forward = new Vector3(-Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius * 0.7f).normalized;
        f.t.SetPositionAndRotation(pos, Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, -18f));
        return f;
    }

    // Short synthesized songbird phrases: a few fast frequency sweeps with vibrato.
    private static AudioClip MakeChirp(int index)
    {
        const int rate = 44100;
        var rng = new System.Random(index * 7919 + 13);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool trill = index % 3 == 2;
        int notes = trill ? 9 : rng.Next(2, 6);
        float noteLength = trill ? 0.045f : R(0.06f, 0.13f);
        float gap = trill ? 0.02f : R(0.04f, 0.1f);
        float total = notes * (noteLength + gap) + 0.1f;
        var data = new float[(int)(total * rate)];
        float baseFreq = R(2600f, 4200f);

        for (int n = 0; n < notes; n++)
        {
            float start = n * (noteLength + gap);
            float f0 = baseFreq * R(0.85f, 1.2f), f1 = f0 * (trill ? 0.8f : R(0.6f, 1.5f));
            double phase = 0;
            int s0 = (int)(start * rate), count = (int)(noteLength * rate);
            for (int s = 0; s < count && s0 + s < data.Length; s++)
            {
                float p = s / (float)count;
                float t = s / (float)rate;
                float freq = Mathf.Lerp(f0, f1, Mathf.Pow(p, 0.6f)) + 140f * Mathf.Sin(2f * Mathf.PI * 32f * t);
                phase += 2.0 * Mathf.PI * freq / rate;
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * p), 1.4f);
                data[s0 + s] += (float)(Mathf.Sin((float)phase) * 0.8f + Mathf.Sin((float)phase * 2f) * 0.15f) * envelope * 0.4f;
            }
        }
        var clip = AudioClip.Create("Chirp_" + index, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
