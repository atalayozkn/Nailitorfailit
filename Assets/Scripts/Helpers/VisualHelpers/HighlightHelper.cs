using System.Collections.Generic;
using Interactions;
using UnityEngine;
using UnityEngine.Rendering;

// Hover highlight for interactables: a soft glow overlay drawn over every mesh of the object.
// The overlay is a hidden child copy of each mesh using NailIt/HoverGlow, so the object's own materials
// (Toon, Lit, warm style, glass...) are never changed or instanced.
public class HighlightHelper : MonoBehaviour
{
    const string OverlayName = "__HoverGlow";
    const string GlowMaterialPath = "Highlight/HoverGlow";

    // Older single-renderer setup. Still used when the object has no interactable above it.
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private bool isMeshRenderer = true;
    [Tooltip("Glow every mesh of the interactable this sits on, not only the assigned renderer.")]
    [SerializeField] private bool includeAllParts = true;

    private static Material glowMaterial;
    private readonly List<(Renderer source, GameObject overlay)> overlays = new();
    private bool built;

    public void HighlightMaterial() => SetGlow(true);
    public void ReverseHighlight() => SetGlow(false);

    private void OnDisable() => SetGlow(false);

    private void SetGlow(bool on)
    {
        if (on && !built) Build();
        foreach (var (source, overlay) in overlays)
        {
            if (overlay == null) continue;
            // A switched-off part (hidden tile variant, hologram) must not glow on its own.
            overlay.SetActive(on && source != null && source.enabled && source.gameObject.activeInHierarchy);
        }
    }

    private void Build()
    {
        built = true;
        if (glowMaterial == null) glowMaterial = Resources.Load<Material>(GlowMaterialPath);
        if (glowMaterial == null)
        {
            Debug.LogWarning($"[HighlightHelper] Missing Resources/{GlowMaterialPath}.mat", this);
            return;
        }
        foreach (var r in Targets())
        {
            var overlay = OverlayFor(r);
            if (overlay != null) overlays.Add((r, overlay));
        }
    }

    // Every mesh that belongs to this interactable; parts owned by a nested interactable glow on their own.
    private List<Renderer> Targets()
    {
        var result = new List<Renderer>();
        var owner = GetComponentInParent<IInteractable>(true) as Component;
        if (includeAllParts && owner != null)
        {
            foreach (var r in owner.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (r.GetComponentInParent<IInteractable>(true) as Component != owner) continue;
                if (r.name == OverlayName) continue;
                result.Add(r);
            }
        }
        if (result.Count == 0)
        {
            Renderer assigned = isMeshRenderer ? meshRenderer : skinnedMeshRenderer;
            if (assigned == null) assigned = isMeshRenderer ? skinnedMeshRenderer : meshRenderer;
            if (assigned != null) result.Add(assigned);
            else
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.name != OverlayName) result.Add(r);
        }
        return result;
    }

    // One overlay per renderer, shared by every helper that targets it.
    private static GameObject OverlayFor(Renderer source)
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
        for (int i = 0; i < materials.Length; i++) materials[i] = glowMaterial;

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
