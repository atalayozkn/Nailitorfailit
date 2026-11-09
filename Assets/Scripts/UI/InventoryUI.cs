using System.Collections.Generic;
using ItemScript;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Bottom-right carry inventory bar: one box per slot with the item's icon, the active slot highlighted.
// Built at runtime on the player's HUD, singleplayer only (see PlayerInteractionHandler.Start).
// Icons come from a hidden camera that renders a mesh-only copy of the item into a render texture per slot,
// once when the slot's content changes, never every frame.
public class InventoryUI : MonoBehaviour
{
    private const string OverlayCanvasName = "OverlayCanvas";

    private const float PanelMargin = 32f;
    private const float SlotSize = 92f;
    private const float SlotSpacing = 10f;
    private const float IconPadding = 8f;
    private const float NumberFontSize = 20f;
    private const float ActiveScale = 1.1f;
    private const float ScaleSpeed = 14f;
    private const int IconResolution = 256;

    private static readonly Color SlotColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color ActiveSlotColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    private static readonly Color ActiveBorderColor = new Color(1f, 0.82f, 0.3f, 1f);
    private static readonly Color NumberColor = new Color(1f, 1f, 1f, 0.75f);

    private class Slot
    {
        public RectTransform rect;
        public Image background;
        public Outline border;
        public RawImage icon;
        public TextMeshProUGUI number;
        public RenderTexture texture;
        public CarriableObject_SP shownItem;
        public bool hasShownItem;
        public int renderAtFrame = -1;
    }

    private PlayerInteractionHandler handler;
    private readonly Slot[] slots = new Slot[PlayerInteractionHandler.InventorySize];
    private IconStudio studio;

    public static void Create(PlayerInteractionHandler handler)
    {
        Canvas overlayCanvas = null;
        foreach (Canvas canvas in handler.transform.root.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.name == OverlayCanvasName) { overlayCanvas = canvas; break; }
        }
        if (overlayCanvas == null)
        {
            Debug.LogWarning($"InventoryUI: no {OverlayCanvasName} found under the player, the inventory bar is disabled.");
            return;
        }

        GameObject panel = new GameObject("InventoryPanel", typeof(RectTransform));
        panel.layer = overlayCanvas.gameObject.layer;
        panel.transform.SetParent(overlayCanvas.transform, false);
        panel.AddComponent<InventoryUI>().Build(handler);
    }

    private void Build(PlayerInteractionHandler interactionHandler)
    {
        handler = interactionHandler;
        studio = new IconStudio(transform.root.name);

        int count = PlayerInteractionHandler.InventorySize;
        RectTransform panelRect = GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-PanelMargin, PanelMargin);
        panelRect.sizeDelta = new Vector2(count * SlotSize + (count - 1) * SlotSpacing, SlotSize);

        for (int i = 0; i < count; i++) slots[i] = CreateSlot(i);

        // The very first render of a fresh camera / texture could come out empty, so get it out of the way now.
        foreach (Slot slot in slots) studio.Clear(slot.texture);
    }

    private Slot CreateSlot(int index)
    {
        Slot slot = new Slot();

        GameObject box = new GameObject($"Slot{index + 1}", typeof(RectTransform));
        box.layer = gameObject.layer;
        slot.rect = box.GetComponent<RectTransform>();
        slot.rect.SetParent(transform, false);
        slot.rect.anchorMin = slot.rect.anchorMax = new Vector2(0f, 0.5f);
        slot.rect.pivot = new Vector2(0.5f, 0.5f);
        slot.rect.sizeDelta = new Vector2(SlotSize, SlotSize);
        slot.rect.anchoredPosition = new Vector2(SlotSize * 0.5f + index * (SlotSize + SlotSpacing), 0f);

        slot.background = box.AddComponent<Image>();
        slot.background.color = SlotColor;
        slot.background.raycastTarget = false;

        slot.border = box.AddComponent<Outline>();
        slot.border.effectColor = ActiveBorderColor;
        slot.border.effectDistance = new Vector2(3f, -3f);
        slot.border.enabled = false;

        slot.texture = new RenderTexture(IconResolution, IconResolution, 24, RenderTextureFormat.ARGB32)
        {
            name = $"InventoryIcon{index + 1}",
            antiAliasing = 4,
        };
        slot.texture.Create();

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
        iconObject.layer = gameObject.layer;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(slot.rect, false);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(IconPadding, IconPadding);
        iconRect.offsetMax = new Vector2(-IconPadding, -IconPadding);
        slot.icon = iconObject.AddComponent<RawImage>();
        slot.icon.texture = slot.texture;
        slot.icon.raycastTarget = false;
        slot.icon.enabled = false;

        GameObject numberObject = new GameObject("Number", typeof(RectTransform));
        numberObject.layer = gameObject.layer;
        RectTransform numberRect = numberObject.GetComponent<RectTransform>();
        numberRect.SetParent(slot.rect, false);
        numberRect.anchorMin = numberRect.anchorMax = numberRect.pivot = new Vector2(0f, 1f);
        numberRect.anchoredPosition = new Vector2(6f, -2f);
        numberRect.sizeDelta = new Vector2(30f, 26f);
        slot.number = numberObject.AddComponent<TextMeshProUGUI>();
        slot.number.text = (index + 1).ToString();
        slot.number.fontSize = NumberFontSize;
        slot.number.fontStyle = FontStyles.Bold;
        slot.number.color = NumberColor;
        slot.number.alignment = TextAlignmentOptions.TopLeft;
        slot.number.raycastTarget = false;

        return slot;
    }

    private void LateUpdate()
    {
        if (handler == null) return;

        int active = handler.GetActiveSlot();
        float blend = 1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime);

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            RefreshIcon(slot, handler.GetSlotItem(i));

            bool isActive = i == active;
            slot.border.enabled = isActive;
            slot.background.color = isActive ? ActiveSlotColor : SlotColor;
            float targetScale = isActive ? ActiveScale : 1f;
            slot.rect.localScale = Vector3.one * Mathf.Lerp(slot.rect.localScale.x, targetScale, blend);
        }
    }

    // Polling three references a frame is cheaper and safer than events: items can also vanish by being destroyed.
    // The photo waits one frame: a spawner creates the item and hands it over in the same frame, before its Start has run.
    private void RefreshIcon(Slot slot, CarriableObject_SP item)
    {
        bool hasItem = item != null;
        if (hasItem != slot.hasShownItem || (hasItem && item != slot.shownItem))
        {
            slot.shownItem = item;
            slot.hasShownItem = hasItem;
            slot.icon.enabled = false;
            slot.renderAtFrame = hasItem ? Time.frameCount + 1 : -1;
        }

        if (slot.renderAtFrame < 0 || Time.frameCount < slot.renderAtFrame) return;
        slot.renderAtFrame = -1;
        slot.icon.enabled = studio.Render(item, slot.texture);
    }

    private void OnDestroy()
    {
        foreach (Slot slot in slots)
        {
            if (slot?.texture == null) continue;
            slot.texture.Release();
            Destroy(slot.texture);
        }
        studio?.Dispose();
    }

    // A camera far below the level that photographs a mesh-only copy of an item.
    // The copy carries no scripts or colliders, so spawners, sounds and gameplay never see it.
    private class IconStudio
    {
        private static readonly Vector3 StagePosition = new Vector3(0f, -5000f, 0f);
        private static readonly Quaternion ViewRotation = Quaternion.Euler(25f, -135f, 0f);
        private const string HoverGlowName = "__HoverGlow";

        private readonly Camera camera;
        private readonly List<Renderer> sources = new List<Renderer>();

        public IconStudio(string ownerName)
        {
            GameObject cameraObject = new GameObject($"InventoryIconCamera ({ownerName})") { hideFlags = HideFlags.DontSave };
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.useOcclusionCulling = false;

            UniversalAdditionalCameraData data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
        }

        public bool Render(CarriableObject_SP item, RenderTexture target)
        {
            item.GetIconRenderers(sources);
            if (sources.Count == 0) return false;

            GameObject copy = BuildCopy(item.transform);
            try
            {
                if (!TryGetBounds(copy, out Bounds bounds)) return false;

                float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
                camera.transform.rotation = ViewRotation;
                camera.transform.position = bounds.center - camera.transform.forward * radius * 3f;
                camera.orthographicSize = radius * 1.05f;
                camera.aspect = 1f;
                camera.nearClipPlane = radius * 0.5f;
                camera.farClipPlane = radius * 5f;

                Submit(target);
                return true;
            }
            finally
            {
                copy.SetActive(false);
                Object.Destroy(copy);
            }
        }

        // Renders the empty stage: a transparent texture, and the camera's first render done ahead of time.
        public void Clear(RenderTexture target)
        {
            camera.transform.SetPositionAndRotation(StagePosition, ViewRotation);
            camera.orthographicSize = 1f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 0.02f;
            Submit(target);
        }

        private void Submit(RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }
        }

        private GameObject BuildCopy(Transform itemRoot)
        {
            GameObject root = new GameObject("InventoryIconCopy") { hideFlags = HideFlags.DontSave };
            root.transform.position = StagePosition;
            Matrix4x4 toItemSpace = itemRoot.worldToLocalMatrix;

            foreach (Renderer source in sources)
            {
                if (source == null || source.name == HoverGlowName) continue;

                Mesh mesh = null;
                if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                else if (source.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;

                GameObject part = new GameObject(source.name) { hideFlags = HideFlags.DontSave };
                Matrix4x4 local = toItemSpace * source.transform.localToWorldMatrix;
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = local.GetColumn(3);
                part.transform.localRotation = local.rotation;
                part.transform.localScale = local.lossyScale;

                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer meshRenderer = part.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = source.sharedMaterials;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
            return root;
        }

        private static bool TryGetBounds(GameObject copy, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (MeshRenderer part in copy.GetComponentsInChildren<MeshRenderer>())
            {
                if (!hasBounds) { bounds = part.bounds; hasBounds = true; }
                else bounds.Encapsulate(part.bounds);
            }
            return hasBounds;
        }

        public void Dispose()
        {
            if (camera != null) Object.Destroy(camera.gameObject);
        }
    }
}
