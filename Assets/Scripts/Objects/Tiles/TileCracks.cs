using System.Collections.Generic;
using UnityEngine;

// Visible cracks on a fragile tile (wood or glass), growing with the damage it takes from being walked on while fragile.
// Added at runtime by the tile on its first pressure hit, so no prefab needs it. The first hit already shows a clear
// crack, and the cracks reach the whole tile on the hit before it breaks. Rebuilding the tile (OnEnable) clears them.
public class TileCracks : MonoBehaviour
{
    public enum Look { Wood, Glass }

    private const string WoodMaterialPath = "Cracks/Crack_Wood";
    private const string GlassMaterialPath = "Cracks/Crack_Glass";
    private const string PuddleChildName = "WaterPuddle";
    private const float FirstHitCrack = 0.3f;
    private static readonly int CrackId = Shader.PropertyToID("_Crack");

    private static Material woodMaterial;
    private static Material glassMaterial;

    private readonly List<Renderer> renderers = new();
    private MaterialPropertyBlock block;
    private Material overlay;
    private int pressureHits;

    // Records one pressure hit that the tile's health has just taken.
    public static void RegisterPressureHit(GameObject tile, Look look, ObjectHealth health)
    {
        if (tile == null || health == null || !tile.activeInHierarchy) return;
        if (!tile.TryGetComponent(out TileCracks cracks)) cracks = tile.AddComponent<TileCracks>();
        cracks.AddHit(look, health);
    }

    private void OnEnable()
    {
        pressureHits = 0;
        ClearOverlay();
    }

    private void AddHit(Look look, ObjectHealth health)
    {
        if (overlay == null) overlay = LoadMaterial(look);
        if (overlay == null) return;

        pressureHits++;
        // Lerp from the first-hit crack to fully cracked on the last hit before the tile breaks.
        int hitsBeforeBreak = Mathf.Max(1, health.MaxHealth - 1);
        float progress = hitsBeforeBreak <= 1 ? 1f : (pressureHits - 1f) / (hitsBeforeBreak - 1f);
        float crack = Mathf.Lerp(FirstHitCrack, 1f, Mathf.Clamp01(progress));
        Apply(crack);
    }

    private void Apply(float crack)
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (renderers.Count == 0) CollectSurfaces();

        block.SetFloat(CrackId, crack);
        foreach (Renderer surface in renderers)
        {
            if (surface == null) continue;
            int slot = OverlaySlots.Add(surface, overlay);
            surface.SetPropertyBlock(block, slot);
        }
    }

    private void ClearOverlay()
    {
        if (overlay != null)
            foreach (Renderer surface in renderers)
                if (surface != null) OverlaySlots.Remove(surface, overlay);
        renderers.Clear();
    }

    // The tile's own visible meshes; the old puddle visual under WaterPuddle and overlays (hover glow) are skipped.
    private void CollectSurfaces()
    {
        Transform puddle = transform.Find(PuddleChildName);
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>())
        {
            if (!candidate.enabled) continue;
            if (!(candidate is MeshRenderer) && !(candidate is SkinnedMeshRenderer)) continue;
            if (candidate.name.StartsWith("__")) continue;
            if (puddle != null && candidate.transform.IsChildOf(puddle)) continue;
            renderers.Add(candidate);
        }
    }

    private static Material LoadMaterial(Look look)
    {
        if (look == Look.Glass)
        {
            if (glassMaterial == null) glassMaterial = Resources.Load<Material>(GlassMaterialPath);
            return glassMaterial;
        }
        if (woodMaterial == null) woodMaterial = Resources.Load<Material>(WoodMaterialPath);
        return woodMaterial;
    }
}
