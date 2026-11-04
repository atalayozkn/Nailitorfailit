using System.Collections.Generic;
using UnityEngine;

// Water soaked up by a built tile (singleplayer). Pipe particles raise the value and every material drains at its own rate.
public class TileWetness : MonoBehaviour
{
    private const string FragileTag = "Fragile";
    private static readonly string[] ColorProperties = { "_BaseColor", "_1st_ShadeColor", "_2nd_ShadeColor" };
    private static readonly List<ParticleCollisionEvent> collisionEvents = new();

    [Header("References")]
    [SerializeField] private Renderer tileRenderer;
    [SerializeField] private PuddleHelper surfaceHelper;

    [Header("Water")]
    [SerializeField] private float gainPerParticle = 0.05f;
    [SerializeField] private float drainPerSecond = 0.05f;
    [SerializeField] private float wetThreshold = 0.5f;
    [SerializeField] private float dryThreshold = 0.3f;

    [Header("Wet Effects")]
    [SerializeField] private bool slipperyWhenWet;
    [SerializeField] private bool fragileWhenWet;
    [SerializeField] private float swellAmount;
    [SerializeField] private float electrifiedDuration = 4f;
    [SerializeField] private Color wetTint = new Color(0.55f, 0.6f, 0.7f, 1f);

    public Renderer TileRenderer => tileRenderer;

    private MaterialPropertyBlock propertyBlock;
    private Color[] dryColors;
    private bool[] hasColor;
    private Vector3 dryScale;
    private string dryTag;
    private float wetness;
    private bool canCollect;
    private bool isWet;
    private bool isElectrified;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        dryScale = tileRenderer != null ? tileRenderer.transform.localScale : Vector3.one;
        dryTag = gameObject.tag;
        dryColors = new Color[ColorProperties.Length];
        hasColor = new bool[ColorProperties.Length];

        if (tileRenderer == null) return;

        Material material = tileRenderer.sharedMaterial;
        for (int i = 0; i < ColorProperties.Length; i++)
        {
            hasColor[i] = material != null && material.HasProperty(ColorProperties[i]);
            if (hasColor[i]) dryColors[i] = material.GetColor(ColorProperties[i]);
        }
    }
    private void OnEnable()
    {
        CancelInvoke(nameof(StopElectrified));
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
        Invoke(nameof(StopElectrified), electrifiedDuration);
    }
    private void StopElectrified()
    {
        isElectrified = false;
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
    }
    private void RefreshSurface()
    {
        if (surfaceHelper == null) return;
        surfaceHelper.SetSurfaceState(isWet && slipperyWhenWet, isElectrified);
    }
    private void RefreshVisual()
    {
        if (tileRenderer == null) return;

        tileRenderer.transform.localScale = new Vector3(dryScale.x, dryScale.y * (1f + swellAmount * wetness), dryScale.z);

        // No block while dry, so dry tiles stay SRP batched.
        if (wetness <= 0f)
        {
            tileRenderer.SetPropertyBlock(null);
            return;
        }

        tileRenderer.GetPropertyBlock(propertyBlock);
        for (int i = 0; i < ColorProperties.Length; i++)
        {
            if (hasColor[i]) propertyBlock.SetColor(ColorProperties[i], Color.Lerp(dryColors[i], dryColors[i] * wetTint, wetness));
        }
        tileRenderer.SetPropertyBlock(propertyBlock);
    }
}
