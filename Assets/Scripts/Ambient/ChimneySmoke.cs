using UnityEngine;

// Purely visual looping chimney smoke. Builds its own ParticleSystem at runtime so the
// look is tuned from this script instead of a hand-edited particle prefab.
// Put it on an empty at the chimney top (e.g. "SmokePoint" in the power plant prefab).
[DisallowMultipleComponent]
public class ChimneySmoke : MonoBehaviour
{
    [SerializeField] private Material smokeMaterial;
    [Tooltip("Flipbook layout of the smoke texture (CFXR 'x4' sheets are 2x2).")]
    [SerializeField] private Vector2Int sheetTiles = new Vector2Int(2, 2);

    [Header("Plume")]
    [SerializeField] private float emissionRate = 6f;
    [SerializeField] private Vector2 lifetime = new Vector2(5f, 7f);
    [SerializeField] private Vector2 riseSpeed = new Vector2(2.5f, 3.5f);
    [SerializeField] private Vector2 startSize = new Vector2(3f, 4.5f);
    [Tooltip("Size multiplier the puff reaches at the end of its life.")]
    [SerializeField] private float endSizeMultiplier = 3f;
    [SerializeField] private float emitRadius = 0.6f;
    [SerializeField] private int maxParticles = 60;

    [Header("Look")]
    [SerializeField] private Color smokeColor = new Color(0.62f, 0.62f, 0.62f, 0.55f);

    [Header("Wind drift (world space, units/s)")]
    [SerializeField] private Vector3 wind = new Vector3(1.2f, 0f, 0.4f);

    private ParticleSystem ps;

    private void Awake()
    {
        if (smokeMaterial == null)
        {
            Debug.LogWarning($"[ChimneySmoke] No material on {name}, smoke disabled.", this);
            enabled = false;
            return;
        }
        Build();
    }

    private void Build()
    {
        var go = new GameObject("ChimneySmoke_FX");
        go.transform.SetParent(transform, false);
        // Point the emitter up regardless of how the building is rotated.
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true; // already smoking when the level loads
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(riseSpeed.x, riseSpeed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize.x, startSize.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = smokeColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = emitRadius;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // Ramp wind in so the plume leaves the chimney straight and bends higher up.
        var ramp = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1f));
        vel.x = new ParticleSystem.MinMaxCurve(wind.x, ramp);
        vel.y = new ParticleSystem.MinMaxCurve(wind.y, ramp);
        vel.z = new ParticleSystem.MinMaxCurve(wind.z, ramp);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = riseSpeed.y + wind.magnitude;
        limit.dampen = 0.08f; // rising smoke slows down as it spreads

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        // Grows from start size (1x) to endSizeMultiplier x by the end of its life.
        float end = Mathf.Max(1f, endSizeMultiplier);
        size.size = new ParticleSystem.MinMaxCurve(end,
            new AnimationCurve(new Keyframe(0f, 1f / end), new Keyframe(1f, 1f)));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        if (sheetTiles.x * sheetTiles.y > 1)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = sheetTiles.x;
            sheet.numTilesY = sheetTiles.y;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            // Random-between-constants: each puff picks one frame and keeps it.
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = smokeMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.OldestInFront;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, emitRadius);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);
    }
#endif
}
