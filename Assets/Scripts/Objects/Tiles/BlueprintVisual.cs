using ItemScript;
using UnityEngine;

// Shows what a floor slot needs through color coding: a see-through copy of the tile's shape in the material's color,
// and (singleplayer) a pulse while the player carries the matching tile.
public class BlueprintVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Constructor constructor;
    [SerializeField] private MeshRenderer ghostRenderer;
    [SerializeField] private MaterialColorChangeHelper hoverHelper;

    [Header("Colors")]
    [SerializeField] private Color woodColor = new Color(1f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color brickColor = new Color(0.9f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color glassColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Header("Alpha")]
    [SerializeField] private float idleAlpha = 0.45f;
    [SerializeField] private float otherMaterialAlpha = 0.2f;
    [SerializeField] private float pulseMinAlpha = 0.35f;
    [SerializeField] private float pulseMaxAlpha = 0.9f;
    [SerializeField] private float pulseSpeed = 5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private PlayerInteractionHandler interactionHandler;
    private Material material;
    private GameObject ghostTile;
    private Color baseColor;
    private bool isReady;
    private bool isHovered;

    private void Start()
    {
        material = ghostRenderer.material;
        if (!NetworkModeHelper.IsNetworked) interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();

        isReady = true;
        Refresh();
    }
    private void Update()
    {
        if (isHovered || interactionHandler == null) return;

        float alpha = idleAlpha;
        if (interactionHandler.IsCarrying())
        {
            alpha = interactionHandler.GetCurrentCarriable() != null
                    && !interactionHandler.GetCurrentCarriable().isRawMaterial
                    && interactionHandler.GetCurrentCarriableType() == constructor.GetAcceptedType()
                ? Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f)
                : otherMaterialAlpha;
        }

        material.SetColor(BaseColorId, new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
    }
    public void SetHovered(bool condition)
    {
        isHovered = condition;
    }
    public void Refresh()
    {
        if (!isReady) return;

        CarriableType type = constructor.GetAcceptedType();

        baseColor = GetColor(type);
        Color idleColor = new Color(baseColor.r, baseColor.g, baseColor.b, idleAlpha);
        material.SetColor(BaseColorId, idleColor);
        if (hoverHelper != null) hoverHelper.SetInitialColor(idleColor);

        RefreshGhostTile(type);
    }
    private void RefreshGhostTile(CarriableType type)
    {
        Renderer source = constructor.GetTileRenderer(type);
        MeshFilter sourceFilter = source != null ? source.GetComponent<MeshFilter>() : null;

        // Keeps the plain slab when the tile has no mesh to copy.
        if (sourceFilter == null)
        {
            ghostRenderer.enabled = true;
            if (ghostTile != null) ghostTile.SetActive(false);
            return;
        }

        if (ghostTile == null)
        {
            ghostTile = new GameObject("GhostTile", typeof(MeshFilter), typeof(MeshRenderer));
            ghostTile.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        ghostTile.SetActive(true);
        ghostTile.transform.SetParent(null);
        ghostTile.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghostTile.transform.localScale = source.transform.lossyScale;
        ghostTile.transform.SetParent(transform, true);

        ghostTile.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
        ghostTile.GetComponent<MeshRenderer>().sharedMaterial = material;

        ghostRenderer.enabled = false;
    }
    private Color GetColor(CarriableType type)
    {
        switch (type)
        {
            case CarriableType.Brick: return brickColor;
            case CarriableType.Glass: return glassColor;
            default: return woodColor;
        }
    }
}
