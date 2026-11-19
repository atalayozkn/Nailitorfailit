using System.Collections.Generic;
using UnityEngine;

// Water soaked up by a built tile (singleplayer). Pipe particles raise the value and every material drains at its own rate.
public class TileWetness : MonoBehaviour
{
    private const string FragileTag = "Fragile";
    private static readonly int WetnessId = Shader.PropertyToID("_Wetness");
    private static readonly List<ParticleCollisionEvent> collisionEvents = new();

    [Header("References")]
    [SerializeField] private Renderer tileRenderer;
    [SerializeField] private PuddleHelper surfaceHelper;

    [Header("Water")]
    [SerializeField] private float gainPerParticle = 0.03f;
    [SerializeField] private float drainPerSecond = 0.05f;
    [SerializeField] private float wetThreshold = 0.5f;
    [SerializeField] private float dryThreshold = 0.3f;

    [Header("Wet Effects")]
    [SerializeField] private bool slipperyWhenWet;
    [SerializeField] private bool fragileWhenWet;
    [SerializeField] private float swellAmount;
    [SerializeField] private float electrifiedDuration = 4f;

    // Multiplayer: every wet tile is slippery, like oil (singleplayer: only the tiles marked slipperyWhenWet, i.e. glass).
    private bool Slippery => slipperyWhenWet || NetworkModeHelper.IsNetworked;

    public Renderer TileRenderer => tileRenderer;
    public float Wetness => wetness;

    private const string PuddleChildName = "WaterPuddle";
    private const string SwellBlendShape = "Sisme";

    // Wood only (tiles with swellAmount > 0): once soaked past wetThreshold the tile is damaged for good. It stays
    // Fragile, and the 'Sisme' blend shape grows while it dries, up to the highest wetness it reached. It never
    // shrinks back; rebuilding the tile (OnEnable) gives a fresh one.
    private SkinnedMeshRenderer swellMesh;
    private int swellShapeIndex = -1;
    private bool isSoaked;
    private float peakWetness;
    private float swellWeight;

    private readonly List<Renderer> visibleRenderers = new();
    private MaterialPropertyBlock propertyBlock;
    private Transform swellTarget;
    private Transform puddleRoot;
    private Vector3 dryScale;
    private string dryTag;
    private GameObject electrifiedEffect;
    private float wetness;
    private bool canCollect;
    private bool isWet;
    private bool isElectrified;
    private bool hasOverlay;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        puddleRoot = transform.Find(PuddleChildName);
        swellTarget = FindSwellTarget();
        dryScale = swellTarget != null ? swellTarget.localScale : Vector3.one;
        dryTag = gameObject.tag;
        FindSwellBlendShape();
    }
    private void FindSwellBlendShape()
    {
        if (swellAmount <= 0f) return;
        foreach (SkinnedMeshRenderer candidate in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!IsSurface(candidate) || candidate.sharedMesh == null) continue;
            int index = candidate.sharedMesh.GetBlendShapeIndex(SwellBlendShape);
            if (index < 0) continue;
            swellMesh = candidate;
            swellShapeIndex = index;
            return;
        }
    }
    private bool UsesSwellBlendShape => swellMesh != null && swellShapeIndex >= 0;
    // Wood and glass tiles draw their surface with a SkinnedMeshRenderer and leave tileRenderer empty, so the swell
    // falls back to the tile's real surface. A skinned mesh follows its root bone, so that is what gets scaled.
    private Transform FindSwellTarget()
    {
        Renderer surface = tileRenderer;
        if (surface == null)
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
                if (IsSurface(candidate)) { surface = candidate; break; }
        if (surface == null) return null;
        if (surface is SkinnedMeshRenderer skinned && skinned.rootBone != null) return skinned.rootBone;
        return surface.transform;
    }
    // The tile's own visible meshes. The old puddle visual under WaterPuddle is part of the tile but isn't its surface.
    private bool IsSurface(Renderer candidate)
    {
        if (!(candidate is MeshRenderer) && !(candidate is SkinnedMeshRenderer)) return false;
        if (candidate.name.StartsWith("__")) return false; // hover glow and other overlays
        if (puddleRoot != null && candidate.transform.IsChildOf(puddleRoot)) return false;
        PuddleHelper puddle = candidate.GetComponentInParent<PuddleHelper>(true);
        return puddle == null || puddle.transform == transform;
    }
    private void OnEnable()
    {
        CancelInvoke(nameof(StopElectrified));
        VfxManager.Stop(electrifiedEffect);
        wetness = 0f;
        canCollect = false;
        isWet = false;
        isElectrified = false;
        // A rebuilt tile is a fresh tile: flat and not damaged.
        isSoaked = false;
        peakWetness = 0f;
        swellWeight = 0f;
        if (UsesSwellBlendShape) swellMesh.SetBlendShapeWeight(swellShapeIndex, 0f);
        gameObject.tag = dryTag;
        RefreshVisual();
    }
    private void Update()
    {
        if (wetness <= 0f) return;

        wetness = Mathf.Max(0f, wetness - drainPerSecond * Time.deltaTime);
        RefreshState();
        RefreshVisual();
    }
    private void OnParticleCollision(GameObject other)
    {
        if (!canCollect && !(NetworkModeHelper.IsNetworked && IsTileComplete())) return;
        if (!other.TryGetComponent<ParticleSystem>(out var particles)) return;

        int hitCount = particles.GetCollisionEvents(gameObject, collisionEvents);
        wetness = Mathf.Clamp01(wetness + hitCount * gainPerParticle);
        RefreshState();
        RefreshVisual();
    }
    // Multiplayer: whether the tile is finished is read from the tile itself. The "can collect" flag is set by one-off
    // calls and is cleared whenever the tile object is switched on again (OnEnable), which left finished tiles dry.
    private bool IsTileComplete()
    {
        if (TryGetComponent(out WoodTile wood)) return wood.GetCurrentPhase() == ConstructionPhase.Complete;
        if (TryGetComponent(out GlassTile glass)) return glass.GetCurrentPhase() == ConstructionPhase.Complete;
        if (TryGetComponent(out BrickTile brick)) return brick.GetCurrentPhase() == ConstructionPhase.Complete;
        return false;
    }

    // Called by the tile once it is nailed down; unfinished tiles don't soak up water.
    public void SetCanCollect(bool condition)
    {
        canCollect = condition;
    }
    public void Electrify()
    {
        if (!isWet || isElectrified) return;

        isElectrified = true;
        RefreshSurface();
        electrifiedEffect = VfxManager.PlayLoop(VfxId.TileElectrified, transform);
        Invoke(nameof(StopElectrified), electrifiedDuration);
    }
    private void StopElectrified()
    {
        isElectrified = false;
        VfxManager.Stop(electrifiedEffect);
        RefreshSurface();
    }
    private void RefreshState()
    {
        bool wasWet = isWet;
        if (wetness >= wetThreshold) isWet = true;
        else if (wetness <= dryThreshold) isWet = false;

        if (swellAmount > 0f)
        {
            if (isWet) isSoaked = true;
            if (isSoaked) peakWetness = Mathf.Max(peakWetness, wetness);
        }

        if (wasWet == isWet) return;

        // Soaked wood stays Fragile after it dries; other tiles are Fragile only while wet.
        if (fragileWhenWet) gameObject.tag = isWet || isSoaked ? FragileTag : dryTag;
        if (Slippery) RefreshSurface();
        if (isWet && swellAmount > 0f) VfxManager.Play(VfxId.WoodSwell, transform.position);
    }
    private void RefreshSurface()
    {
        if (surfaceHelper == null) return;
        surfaceHelper.SetSurfaceState(isWet && Slippery, isElectrified);
    }
    private void RefreshVisual()
    {
        if (UsesSwellBlendShape) RefreshSwellBlendShape();
        else if (swellTarget != null && swellAmount > 0f) swellTarget.localScale = new Vector3(dryScale.x, dryScale.y * (1f + swellAmount * wetness), dryScale.z);

        // The overlay is only on the renderers while wet, so dry tiles stay batched.
        SetOverlay(wetness > 0f);
        if (!hasOverlay) return;

        propertyBlock.SetFloat(WetnessId, wetness);
        Material overlay = VfxManager.WetOverlayMaterial;
        foreach (Renderer visible in visibleRenderers)
        {
            int slot = OverlaySlots.IndexOf(visible, overlay);
            if (slot >= 0) visible.SetPropertyBlock(propertyBlock, slot);
        }
    }
    // The swell grows as the soaked tile dries (drying progress x highest wetness reached) and never goes back.
    private void RefreshSwellBlendShape()
    {
        if (!isSoaked || peakWetness <= 0f) return;
        float dried = 1f - Mathf.Clamp01(wetness / peakWetness);
        swellWeight = Mathf.Max(swellWeight, dried * peakWetness * 100f);
        swellMesh.SetBlendShapeWeight(swellShapeIndex, swellWeight);
    }
    private void SetOverlay(bool condition)
    {
        if (condition == hasOverlay) return;

        Material overlay = VfxManager.WetOverlayMaterial;
        if (overlay == null) return;

        if (condition) AddOverlay(overlay);
        else RemoveOverlay(overlay);

        hasOverlay = condition;
    }
    // Goes on the meshes the tile really shows: FloorConstruct swaps in its own brick mesh, and wood/glass use a skinned mesh.
    // Other overlays (cracks) can share the renderer, so slots are handled by material through OverlaySlots.
    private void AddOverlay(Material overlay)
    {
        visibleRenderers.Clear();
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>())
        {
            if (!candidate.enabled || !IsSurface(candidate)) continue;
            OverlaySlots.Add(candidate, overlay);
            visibleRenderers.Add(candidate);
        }
    }
    private void RemoveOverlay(Material overlay)
    {
        foreach (Renderer visible in visibleRenderers)
            if (visible != null) OverlaySlots.Remove(visible, overlay);

        visibleRenderers.Clear();
    }
}
