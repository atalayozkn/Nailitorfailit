using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Keeps small gameplay objects (sockets, pipes) readable when the roof, a wall or a floor tile covers them.
// Each mesh gets a hidden child copy drawn with NailIt/XRaySilhouette, which only shows where something is in front.
// Idle it is a faint tint; SetWarning pulses it in the warning colour (used by TelegraphedHazard).
public class XRayOverlay : MonoBehaviour
{
    const string OverlayName = "__XRay";
    const string MaterialPath = "Highlight/XRaySilhouette";

    [SerializeField] private Color idleColor = new Color(1f, 0.95f, 0.85f, 0.22f);
    [SerializeField] private Color warningColor = new Color(1f, 0.55f, 0.1f, 0.85f);
    [SerializeField] private float pulseSpeed = 8f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material material;
    private readonly List<Renderer> overlays = new();
    private MaterialPropertyBlock block;
    private bool isWarning;

    private void Awake()
    {
        if (material == null) material = Resources.Load<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"[XRayOverlay] Missing Resources/{MaterialPath}.mat", this);
            enabled = false;
            return;
        }
        block = new MaterialPropertyBlock();
        foreach (var source in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (source.name.StartsWith("__")) continue;
            var overlay = Build(source);
            if (overlay != null) overlays.Add(overlay);
        }
        Apply(idleColor);
    }
    private void Update()
    {
        if (!isWarning) return;
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Apply(Color.Lerp(idleColor, warningColor, 0.35f + 0.65f * pulse));
    }

    public void SetWarning(bool warning)
    {
        isWarning = warning;
        if (!warning && block != null) Apply(idleColor);
    }

    private void Apply(Color color)
    {
        block.SetColor(ColorId, color);
        foreach (var overlay in overlays)
            if (overlay != null) overlay.SetPropertyBlock(block);
    }

    private static Renderer Build(MeshRenderer source)
    {
        if (source.isPartOfStaticBatch) return null; // its mesh is the whole batch
        var filter = source.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return null;

        var go = new GameObject(OverlayName) { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
        go.transform.SetParent(source.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        var copy = go.AddComponent<MeshRenderer>();
        var materials = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        copy.sharedMaterials = materials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return copy;
    }
}
