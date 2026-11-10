using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Shows a floor slot as a hologram of the real tile. An empty slot draws a copy of the tile's meshes; once a tile is placed
// its own renderers wear the hologram, which dissolves as it gets nailed down and gives the real materials back when finished.
// Reads the tile every frame instead of being told, because multiplayer places and completes tiles through its own path.
public class BlueprintVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ItemScript.Constructor constructor;
    [SerializeField] private MeshRenderer ghostRenderer;
    [SerializeField] private Material hologramMaterial;

    [Header("Empty Slot")]
    [SerializeField] private float pulseMaxHighlight = 0.6f;
    [SerializeField] private float pulseSpeed = 5f;
    [Tooltip("Singleplayer: an empty slot is hidden unless the player carries its processed tile. Looking at it shows it this faintly.")]
    [SerializeField, Range(0f, 1f)] private float hoverOnlyAlpha = 0.35f;
    [SerializeField] private float fadeSpeed = 4f;

    [Header("Reveal")]
    [SerializeField, Range(0f, 1f)] private float placedReveal = 0.2f;
    [SerializeField] private float revealSpeed = 2f;

    [Header("Highlight")]
    [SerializeField] private float highlightSpeed = 8f;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int AlphaScaleId = Shader.PropertyToID("_AlphaScale");
    private static readonly int HighlightId = Shader.PropertyToID("_Highlight");

    private readonly List<GameObject> ghostParts = new();
    private readonly List<Material> ghostMaterials = new();
    private readonly List<Material> tileMaterials = new();
    private readonly List<(MeshRenderer renderer, Material[] original)> swappedRenderers = new();

    private PlayerInteractionHandler interactionHandler;
    private GameObject currentTile;
    private IBuildableTile currentBuildable;
    private float reveal;
    private float highlight;
    private bool isReady;
    private bool isHovered;
    private float ghostAlpha;
    private bool ghostVisible = true;

    private void Start()
    {
        if (!NetworkModeHelper.IsNetworked) interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();

        isReady = true;
        Refresh();
    }
    private void OnDestroy()
    {
        DestroyMaterials(ghostMaterials);
        DestroyMaterials(tileMaterials);
    }
    // Late so a tile placed this frame never renders once with its real materials.
    private void LateUpdate()
    {
        if (!isReady) return;

        GameObject tile = constructor.GetActiveTile();
        if (tile != currentTile) OnTileChanged(tile);

        if (currentTile == null) UpdateGhost();
        else if (swappedRenderers.Count > 0) UpdatePlacedTile();
    }
    public void SetHovered(bool condition)
    {
        isHovered = condition;
    }
    public void Refresh()
    {
        if (!isReady) return;

        RebuildGhost();
    }
    private void UpdateGhost()
    {
        // Multiplayer has no local handler here and keeps the always-visible blueprint.
        if (interactionHandler == null)
        {
            UpdateGhostAlwaysVisible();
            return;
        }

        bool matches = interactionHandler.IsCarrying()
            && interactionHandler.GetCurrentCarriable() != null
            && !interactionHandler.GetCurrentCarriable().isRawMaterial
            && interactionHandler.GetCurrentCarriableType() == constructor.GetAcceptedType();

        float targetAlpha = matches ? 1f : isHovered ? hoverOnlyAlpha : 0f;
        float target = 0f;
        if (matches) target = isHovered ? 1f : (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f * pulseMaxHighlight;

        ghostAlpha = Mathf.MoveTowards(ghostAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
        highlight = Mathf.MoveTowards(highlight, target, highlightSpeed * Time.deltaTime);
        SetGhostVisible(ghostAlpha > 0.001f);
        ApplyTo(ghostMaterials, 0f, ghostAlpha, highlight);
    }
    private void UpdateGhostAlwaysVisible()
    {
        float target = isHovered ? 1f : 0f;
        highlight = Mathf.MoveTowards(highlight, target, highlightSpeed * Time.deltaTime);
        SetGhostVisible(true);
        ApplyTo(ghostMaterials, 0f, 1f, highlight);
    }
    // Hidden slots are switched off, not just drawn at zero alpha, so they cost nothing.
    private void SetGhostVisible(bool visible)
    {
        if (visible == ghostVisible) return;
        ghostVisible = visible;
        foreach (GameObject part in ghostParts) part.SetActive(visible);
        ghostRenderer.enabled = visible && ghostParts.Count == 0;
    }
    private void UpdatePlacedTile()
    {
        if (currentBuildable.IsBuildComplete)
        {
            RestoreTile();
            isHovered = false;
            highlight = 0f;
            return;
        }

        float target = Mathf.Lerp(placedReveal, 1f, currentBuildable.BuildProgress);
        reveal = Mathf.MoveTowards(reveal, target, revealSpeed * Time.deltaTime);
        highlight = Mathf.MoveTowards(highlight, isHovered ? 1f : 0f, highlightSpeed * Time.deltaTime);
        ApplyTo(tileMaterials, reveal, 1f, highlight);
    }
    private void OnTileChanged(GameObject tile)
    {
        RestoreTile();

        currentTile = tile;
        currentBuildable = tile != null ? tile.GetComponent<IBuildableTile>() : null;
        reveal = 0f;
        highlight = 0f;

        if (currentBuildable != null && !currentBuildable.IsBuildComplete) HologramiseTile(tile);
    }
    private void HologramiseTile(GameObject tile)
    {
        foreach (MeshRenderer visible in GetVisibleRenderers(tile))
        {
            Material[] original = visible.sharedMaterials;
            Material[] holograms = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                holograms[i] = CreateHologram(original[i]);
                tileMaterials.Add(holograms[i]);
            }

            swappedRenderers.Add((visible, original));
            visible.sharedMaterials = holograms;
        }
    }
    private void RestoreTile()
    {
        foreach (var (renderer, original) in swappedRenderers)
        {
            if (renderer != null) renderer.sharedMaterials = original;
        }

        swappedRenderers.Clear();
        DestroyMaterials(tileMaterials);
    }
    private void RebuildGhost()
    {
        foreach (GameObject part in ghostParts) Destroy(part);
        ghostParts.Clear();
        DestroyMaterials(ghostMaterials);

        GameObject tile = constructor.GetTileObject(constructor.GetAcceptedType());
        if (tile != null)
        {
            foreach (MeshRenderer source in GetVisibleRenderers(tile)) CreateGhostPart(source);
        }

        // Keeps the plain slab when the tile has no mesh to copy.
        ghostRenderer.enabled = ghostParts.Count == 0;
        ghostVisible = true;
        if (ghostRenderer.enabled)
        {
            Material slab = CreateHologram(null);
            ghostMaterials.Add(slab);
            ghostRenderer.sharedMaterial = slab;
        }
    }
    private void CreateGhostPart(MeshRenderer source)
    {
        if (!source.TryGetComponent<MeshFilter>(out var sourceFilter) || sourceFilter.sharedMesh == null) return;

        // A statically batched tile only has the merged mesh of its whole batch left to copy.
        if (source.isPartOfStaticBatch)
        {
            Debug.LogWarning($"{source.name} is static batched, so its blueprint falls back to a slab. Clear Batching Static on the tile mesh.", source);
            return;
        }

        GameObject part = new GameObject("GhostTile", typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        part.transform.localScale = source.transform.lossyScale;
        part.transform.SetParent(ghostRenderer.transform, true);
        part.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;

        Material[] sources = source.sharedMaterials;
        Material[] holograms = new Material[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            holograms[i] = CreateHologram(sources[i]);
            ghostMaterials.Add(holograms[i]);
        }

        MeshRenderer partRenderer = part.GetComponent<MeshRenderer>();
        partRenderer.shadowCastingMode = ShadowCastingMode.Off;
        partRenderer.sharedMaterials = holograms;
        ghostParts.Add(part);
    }
    private Material CreateHologram(Material source)
    {
        Material hologram = new Material(hologramMaterial);
        if (source == null) return hologram;

        int textureId = source.HasProperty(BaseMapId) ? BaseMapId : MainTexId;
        if (source.HasProperty(textureId) && source.GetTexture(textureId) != null)
        {
            hologram.SetTexture(BaseMapId, source.GetTexture(textureId));
            hologram.SetTextureScale(BaseMapId, source.GetTextureScale(textureId));
            hologram.SetTextureOffset(BaseMapId, source.GetTextureOffset(textureId));
        }
        if (source.HasProperty(BaseColorId)) hologram.SetColor(BaseColorId, source.GetColor(BaseColorId));

        return hologram;
    }
    // The meshes the tile really shows: FloorConstruct swaps in its own brick mesh, so TileWetness.TileRenderer can be missing.
    private static List<MeshRenderer> GetVisibleRenderers(GameObject tile)
    {
        List<MeshRenderer> visible = new();
        foreach (MeshRenderer candidate in tile.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!candidate.enabled || !candidate.gameObject.activeSelf) continue;

            // The old puddle plane is part of the tile but isn't its surface.
            PuddleHelper puddle = candidate.GetComponentInParent<PuddleHelper>(true);
            if (puddle != null && puddle.transform != tile.transform) continue;

            visible.Add(candidate);
        }
        return visible;
    }
    private static void ApplyTo(List<Material> materials, float revealAmount, float alphaScale, float highlightAmount)
    {
        foreach (Material material in materials)
        {
            material.SetFloat(RevealId, revealAmount);
            material.SetFloat(AlphaScaleId, alphaScale);
            material.SetFloat(HighlightId, highlightAmount);
        }
    }
    private static void DestroyMaterials(List<Material> materials)
    {
        foreach (Material material in materials) Destroy(material);
        materials.Clear();
    }
}
