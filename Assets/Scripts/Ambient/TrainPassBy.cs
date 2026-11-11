using System.Collections;
using UnityEngine;

// Purely visual ambient train. Put it on the parent that holds the rails and the train
// (e.g. "tren ve raylar 1"). The track line is worked out from the rail pieces, the train
// drives from one end to the other, hides, waits a random delay, and comes again.
// The train never collides with or affects the player: its colliders are switched off.
// Not networked: each client runs its own copy, which is fine for pure decoration.
public class TrainPassBy : MonoBehaviour
{
    [Tooltip("Moving part. Left empty, the first child whose name contains 'train' is used.")]
    [SerializeField] private Transform train;

    [Header("Timing")]
    [SerializeField] private float speed = 14f;
    [SerializeField] private Vector2 waitBetweenPasses = new Vector2(10f, 25f);
    [Tooltip("Random wait before the very first pass so several trains don't run in sync.")]
    [SerializeField] private Vector2 firstPassDelay = new Vector2(1f, 12f);

    [Header("Path")]
    [Tooltip("Flip if the train drives backwards.")]
    [SerializeField] private bool reverseDirection;
    [Tooltip("Extra distance past each track end (negative keeps the train inside the track).")]
    [SerializeField] private float extendBeyondTrack;

    [Header("Sound (3D, follows the train)")]
    [SerializeField] private AudioClip rollingLoop;
    [SerializeField, Range(0f, 1f)] private float rollingVolume = 0.8f;
    [SerializeField] private float rollingMaxDistance = 50f;
    [Tooltip("Played once each time the train enters the map.")]
    [SerializeField] private AudioClip whistle;
    [SerializeField, Range(0f, 1f)] private float whistleVolume = 1f;
    [SerializeField] private float whistleMaxDistance = 120f;
    [SerializeField] private float fadeTime = 0.6f;

    private AudioSource rollingSource;
    private AudioSource whistleSource;
    private Vector3 startPos;
    private Vector3 endPos;
    private Renderer[] trainRenderers;
    private bool ready;

    private void Start()
    {
        if (train == null) train = FindTrainChild();
        if (train == null)
        {
            Debug.LogWarning($"[TrainPassBy] No train child under {name}.", this);
            enabled = false;
            return;
        }

        foreach (var c in train.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        trainRenderers = train.GetComponentsInChildren<Renderer>(true);

        if (!BuildPath())
        {
            Debug.LogWarning($"[TrainPassBy] Could not find rail pieces under {name}.", this);
            enabled = false;
            return;
        }

        // Created directly rather than through Sfx so the trains are heard in multiplayer too.
        if (rollingLoop != null)
        {
            rollingSource = CreateSource(rollingLoop, rollingMaxDistance, 0.5f);
            rollingSource.loop = true;
        }
        if (whistle != null) whistleSource = CreateSource(whistle, whistleMaxDistance, 0f);

        ready = true;
        SetVisible(false);
        StartCoroutine(Loop());
    }

    private Transform FindTrainChild()
    {
        foreach (Transform t in transform)
            if (t.name.ToLowerInvariant().Contains("train")) return t;
        return null;
    }

    // Track = the two rail pieces farthest apart. The train keeps its own height and
    // sideways offset from that line and only slides along it.
    private bool BuildPath()
    {
        var rails = new System.Collections.Generic.List<Vector3>();
        foreach (Transform t in transform)
            if (t != train && t.name.ToLowerInvariant().Contains("rail")) rails.Add(t.position);
        if (rails.Count < 2) // rail pieces renamed: fall back to every other child
        {
            rails.Clear();
            foreach (Transform t in transform) if (t != train) rails.Add(t.position);
        }
        if (rails.Count < 2) return false;

        Vector3 a = rails[0], b = rails[1];
        float best = -1f;
        for (int i = 0; i < rails.Count; i++)
            for (int j = i + 1; j < rails.Count; j++)
            {
                Vector3 d = rails[j] - rails[i]; d.y = 0f;
                float m = d.sqrMagnitude;
                if (m > best) { best = m; a = rails[i]; b = rails[j]; }
            }

        Vector3 axis = b - a; axis.y = 0f;
        if (axis.sqrMagnitude < 0.01f) return false;
        axis.Normalize();

        // Drive in the direction the train is facing (its most track-aligned local axis).
        Vector3 facing = MostAlignedAxis(train, axis);
        if (Vector3.Dot(facing, axis) < 0f) axis = -axis;
        if (reverseDirection) axis = -axis;

        float halfLen = TrainHalfLength(axis);
        Vector3 origin = train.position;
        float tA = Vector3.Dot(a - origin, axis);
        float tB = Vector3.Dot(b - origin, axis);
        float tMin = Mathf.Min(tA, tB) + halfLen - extendBeyondTrack;
        float tMax = Mathf.Max(tA, tB) - halfLen + extendBeyondTrack;
        if (tMax <= tMin) { tMin = Mathf.Min(tA, tB); tMax = Mathf.Max(tA, tB); }

        startPos = origin + axis * tMin;
        endPos = origin + axis * tMax;
        return true;
    }

    private static Vector3 MostAlignedAxis(Transform t, Vector3 axis)
    {
        Vector3 best = t.forward;
        float bestDot = Mathf.Abs(Vector3.Dot(t.forward, axis));
        float r = Mathf.Abs(Vector3.Dot(t.right, axis));
        if (r > bestDot) best = t.right;
        return best;
    }

    private float TrainHalfLength(Vector3 axis)
    {
        if (trainRenderers.Length == 0) return 0f;
        float min = float.MaxValue, max = float.MinValue;
        foreach (var r in trainRenderers)
        {
            Bounds bb = r.bounds;
            Vector3 c = bb.center, e = bb.extents;
            float proj = Vector3.Dot(c, axis);
            float ext = Mathf.Abs(e.x * axis.x) + Mathf.Abs(e.z * axis.z);
            min = Mathf.Min(min, proj - ext);
            max = Mathf.Max(max, proj + ext);
        }
        return (max - min) * 0.5f;
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(firstPassDelay.x, firstPassDelay.y));
        float dist = Vector3.Distance(startPos, endPos);
        while (true)
        {
            train.position = startPos;
            SetVisible(true);
            if (whistleSource != null) whistleSource.PlayOneShot(whistle, whistleVolume);
            if (rollingSource != null)
            {
                rollingSource.time = Random.Range(0f, rollingLoop.length);
                rollingSource.volume = 0f;
                rollingSource.Play();
            }

            float t = 0f;
            float dur = dist / Mathf.Max(0.1f, speed);
            float fade = Mathf.Min(fadeTime, dur * 0.5f);
            while (t < dur)
            {
                t += Time.deltaTime;
                train.position = Vector3.Lerp(startPos, endPos, t / dur);
                if (rollingSource != null)
                    rollingSource.volume = rollingVolume * Mathf.Clamp01(Mathf.Min(t, dur - t) / Mathf.Max(0.01f, fade));
                yield return null;
            }
            if (rollingSource != null) rollingSource.Stop();
            SetVisible(false);
            yield return new WaitForSeconds(Random.Range(waitBetweenPasses.x, waitBetweenPasses.y));
        }
    }

    private AudioSource CreateSource(AudioClip clip, float maxDistance, float doppler)
    {
        var s = train.gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.outputAudioMixerGroup = Sfx.Group; // respects the SFX volume slider
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear; // silent past maxDistance
        s.minDistance = maxDistance * 0.15f;
        s.maxDistance = maxDistance;
        s.dopplerLevel = doppler; // slight pitch shift as it passes
        return s;
    }

    private void SetVisible(bool v)
    {
        foreach (var r in trainRenderers) if (r != null) r.enabled = v;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!ready) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(startPos, endPos);
        Gizmos.DrawWireSphere(startPos, 1f);
        Gizmos.DrawSphere(endPos, 1f);
    }
#endif
}
