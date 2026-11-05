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

    public Renderer TileRenderer => tileRenderer;
    public float Wetness => wetness;

    private readonly List<MeshRenderer> visibleRenderers = new();
    private MaterialPropertyBlock propertyBlock;
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
        dryScale = tileRenderer != null ? tileRenderer.transform.localScale : Vector3.one;
        dryTag = gameObject.tag;
    }
    private void OnEnable()
    {
        CancelInvoke(nameof(StopElectrified));
        VfxManager.Stop(electrifiedEffect);
        wetness = 0f;
        canCollect = false;
        isWet = false;
        isElectrified = false;
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
        if (!canCollect || NetworkModeHelper.IsNetworked) return;
        if (!other.TryGetComponent<ParticleSystem>(out var particles)) return;

        int hitCount = particles.GetCollisionEvents(gameObject, collisionEvents);
        wetness = Mathf.Clamp01(wetness + hitCount * gainPerParticle);
        RefreshState();
        RefreshVisual();
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

        if (wasWet == isWet) return;

        if (fragileWhenWet) gameObject.tag = isWet ? FragileTag : dryTag;
        if (slipperyWhenWet) RefreshSurface();
        if (isWet && swellAmount > 0f) VfxManager.Play(VfxId.WoodSwell, transform.position);
    }
    private void RefreshSurface()
    {
        if (surfaceHelper == null) return;
        surfaceHelper.SetSurfaceState(isWet && slipperyWhenWet, isElectrified);
    }
    private void RefreshVisual()
    {
        if (tileRenderer != null) tileRenderer.transform.localScale = new Vector3(dryScale.x, dryScale.y * (1f + swellAmount * wetness), dryScale.z);

        // The overlay is only on the renderers while wet, so dry tiles stay batched.
        SetOverlay(wetness > 0f);
        if (!hasOverlay) return;

        propertyBlock.SetFloat(WetnessId, wetness);
        foreach (MeshRenderer visible in visibleRenderers) visible.SetPropertyBlock(propertyBlock, visible.sharedMaterials.Length - 1);
    }
    private void SetOverlay(bool condition)
    {
        if (condition == hasOverlay) return;

        Material overlay = VfxManager.WetOverlayMaterial;
        if (overlay == null) return;

        if (condition) AddOverlay(overlay);
        else RemoveOverlay();

        hasOverlay = condition;
    }
    // Goes on the meshes the tile really shows: FloorConstruct swaps in its own brick mesh, so tileRenderer alone can be hidden.
    private void AddOverlay(Material overlay)
    {
        visibleRenderers.Clear();
        foreach (MeshRenderer candidate in GetComponentsInChildren<MeshRenderer>())
        {
            if (!candidate.enabled) continue;
            // The old puddle plane is part of the tile but isn't its surface.
            if (candidate.GetComponentInParent<PuddleHelper>() != null && candidate.GetComponentInParent<PuddleHelper>().transform != transform) continue;

            Material[] current = candidate.sharedMaterials;
            Material[] materials = new Material[current.Length + 1];
            current.CopyTo(materials, 0);
            materials[current.Length] = overlay;
            candidate.sharedMaterials = materials;
            visibleRenderers.Add(candidate);
        }
    }
    private void RemoveOverlay()
    {
        foreach (MeshRenderer visible in visibleRenderers)
        {
            if (visible == null) continue;

            Material[] current = visible.sharedMaterials;
            visible.SetPropertyBlock(null, current.Length - 1);
            Material[] materials = new Material[current.Length - 1];
            System.Array.Copy(current, materials, materials.Length);
            visible.sharedMaterials = materials;
        }

        visibleRenderers.Clear();
    }
}
