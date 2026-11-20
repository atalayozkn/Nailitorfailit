using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Attention glimmer: a strong pulsing glow with a shine band sliding across it, used to point the player at the
// next thing to press (the mixer handle once all 3 ingredients are in).
// Separate from HighlightHelper on purpose: it has its own overlay objects and material, so hovering on/off the
// object never switches it off, and it reads differently from the plain hover glow.
public class AttentionGlimmer : MonoBehaviour
{
    const string OverlayName = "__AttentionGlimmer";
    const string BaseMaterialPath = "Highlight/HoverGlow";

    [SerializeField] private Color color = new Color(1f, 0.93f, 0.55f, 1f);
    [Tooltip("Renderers to glimmer. Empty = the parts the HighlightHelper on this object glows, else every child mesh.")]
    [SerializeField] private List<Renderer> targets = new();

    private static Material glimmerMaterial;
    private readonly List<(Renderer source, GameObject overlay)> overlays = new();
    private bool built;
    private bool on;

    public bool IsOn => on;

    public void SetOn(bool value)
    {
        if (on == value && built) return;
        on = value;
        Apply();
    }

    private void OnEnable() { if (on) Apply(); }
    private void OnDisable()
    {
        foreach (var (_, overlay) in overlays) if (overlay != null) overlay.SetActive(false);
    }

    private void LateUpdate()
    {
        // A part can be hidden or shown while the glimmer runs; keep the overlays in step with their source.
        if (!on) return;
        foreach (var (source, overlay) in overlays)
        {
            if (overlay == null) continue;
            bool visible = source != null && source.enabled && source.gameObject.activeInHierarchy;
            if (overlay.activeSelf != visible) overlay.SetActive(visible);
        }
    }

    private void Apply()
    {
        if (on && !built) Build();
        foreach (var (source, overlay) in overlays)
        {
            if (overlay == null) continue;
            overlay.SetActive(on && source != null && source.enabled && source.gameObject.activeInHierarchy);
        }
    }

    private void Build()
    {
        built = true;
        Material material = MaterialFor(color);
        if (material == null) return;

        List<Renderer> list = targets != null && targets.Count > 0 ? targets : null;
        if (list == null)
        {
            var helper = GetComponent<HighlightHelper>();
            if (helper != null) list = helper.GlowTargets();
        }
        if (list == null || list.Count == 0)
        {
            list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if ((r is MeshRenderer || r is SkinnedMeshRenderer) && !r.name.StartsWith("__")) list.Add(r);
        }

        foreach (Renderer r in list)
        {
            if (r == null) continue;
            GameObject overlay = CreateOverlay(r, material);
            if (overlay != null) overlays.Add((r, overlay));
        }
        if (overlays.Count == 0)
            Debug.LogWarning($"[AttentionGlimmer] Nothing to glimmer on {name}. Static-batched meshes cannot get an " +
                             "overlay: untick Batching Static on that model.", this);
    }

    private static Material MaterialFor(Color color)
    {
        if (glimmerMaterial != null) return glimmerMaterial;
        var source = Resources.Load<Material>(BaseMaterialPath);
        if (source == null)
        {
            Debug.LogWarning($"[AttentionGlimmer] Missing Resources/{BaseMaterialPath}.mat");
            return null;
        }
        glimmerMaterial = new Material(source) { name = "AttentionGlimmer (runtime)" };
        glimmerMaterial.SetColor("_GlowColor", color);
        glimmerMaterial.SetFloat("_Fill", 0.55f);
        glimmerMaterial.SetFloat("_RimStrength", 2.2f);
        glimmerMaterial.SetFloat("_RimPower", 1.8f);
        glimmerMaterial.SetFloat("_PulseSpeed", 5f);
        glimmerMaterial.SetFloat("_PulseAmount", 0.7f);
        glimmerMaterial.SetFloat("_Inflate", 0.008f);
        glimmerMaterial.SetFloat("_SweepStrength", 2.5f);
        glimmerMaterial.SetFloat("_SweepSpeed", 0.9f);
        glimmerMaterial.SetFloat("_SweepScale", 1.5f);
        glimmerMaterial.SetFloat("_SweepWidth", 0.2f);
        glimmerMaterial.renderQueue = 3020; // over the hover glow (Transparent+10)
        return glimmerMaterial;
    }

    private static GameObject CreateOverlay(Renderer source, Material material)
    {
        var existing = source.transform.Find(OverlayName);
        if (existing != null) return existing.gameObject;

        Mesh mesh;
        if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        else
        {
            if (source.isPartOfStaticBatch) return null; // its mesh is the combined batch, a copy would draw the whole batch
            var filter = source.GetComponent<MeshFilter>();
            mesh = filter != null ? filter.sharedMesh : null;
        }
        if (mesh == null) return null;

        var go = new GameObject(OverlayName) { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
        go.SetActive(false);
        go.transform.SetParent(source.transform, false);

        var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;

        Renderer copy;
        if (source is SkinnedMeshRenderer s)
        {
            var sk = go.AddComponent<SkinnedMeshRenderer>();
            sk.sharedMesh = mesh;
            sk.bones = s.bones;
            sk.rootBone = s.rootBone;
            sk.localBounds = s.localBounds;
            sk.updateWhenOffscreen = s.updateWhenOffscreen;
            copy = sk;
        }
        else
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            copy = go.AddComponent<MeshRenderer>();
        }
        copy.sharedMaterials = materials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return go;
    }
}
