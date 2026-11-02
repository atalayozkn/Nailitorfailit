using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Settings card with Audio / Display / Graphics tabs, drawn on the same board art as the info pop-ups.
// The UI is built in code; values live in GameSettings and are saved as soon as they change.
public class SettingsMenu : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private Sprite boardSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color textColor = new Color(0.25f, 0.15f, 0.08f, 1f);
    [SerializeField] private Color tabColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("Volume Slider (Art/UI/Settings)")]
    [SerializeField] private Sprite sliderPlate;
    [SerializeField] private Sprite sliderTrack;
    [SerializeField] private Sprite sliderFill;
    [SerializeField] private Sprite sliderHandle;

    [Header("Events")]
    [SerializeField] private UnityEvent onClosed;

    private GameObject root;
    private readonly List<GameObject> pages = new List<GameObject>();
    private readonly List<Image> tabImages = new List<Image>();
    private readonly List<Action> refreshers = new List<Action>();
    private int currentTab;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        BuildUI();
        root.SetActive(false);
    }
    private void OnEnable() => GameSettings.Changed += Refresh;
    private void OnDisable() => GameSettings.Changed -= Refresh;
    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    #region Open / Close

    public void Open()
    {
        root.SetActive(true);
        SelectTab(currentTab);
        Refresh();
    }
    public void Close()
    {
        if (!IsOpen) return;
        root.SetActive(false);
        onClosed?.Invoke();
    }

    private void SelectTab(int index)
    {
        currentTab = index;
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].SetActive(i == index);
            tabImages[i].color = i == index ? Color.white : tabColor;
        }
    }

    private void Refresh()
    {
        foreach (var refresh in refreshers) refresh();
    }

    #endregion

    #region UI Building

    // Regions of the board art (Art/UI/1/5.png) as fractions of the image, bottom-left origin (same art as InfoPopup).
    private static readonly Rect PlankTitleArea = new Rect(0.42f, 0.80f, 0.30f, 0.13f);
    private static readonly Rect TabsArea = new Rect(0.12f, 0.635f, 0.77f, 0.085f);
    private static readonly Rect ContentArea = new Rect(0.12f, 0.25f, 0.77f, 0.37f);
    private static readonly Rect ResetButtonArea = new Rect(0.12f, 0.13f, 0.22f, 0.10f);
    private static readonly Rect BackButtonArea = new Rect(0.70f, 0.13f, 0.19f, 0.10f);
    private const float BoardAspect = 2048f / 1357f;
    private const float PlankTilt = 2.5f;
    private const int RowsPerPage = 4;

    // Bar inside the slider plate art, as fractions of the plate.
    private static readonly Rect PlateBarArea = new Rect(0.222f, 0.362f, 0.704f, 0.249f);
    private const float PlateAspect = 2492f / 555f;

    private void BuildUI()
    {
        root = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dim = CreateImage("Dim", root.transform, null, dimColor);
        Stretch(dim.rectTransform);

        var board = CreateImage("Board", root.transform, boardSprite, boardSprite != null ? Color.white : new Color(0.96f, 0.88f, 0.74f, 1f));
        board.rectTransform.anchorMin = board.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        board.rectTransform.sizeDelta = new Vector2(1320f, 1320f / BoardAspect);

        var title = CreateText("Title", board.transform, "Settings", 44, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, PlankTitleArea);
        title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, PlankTilt);

        var tabs = CreateRect("Tabs", board.transform);
        Anchor(tabs, TabsArea);
        var content = CreateRect("Content", board.transform);
        Anchor(content, ContentArea);

        string[] tabNames = { "Audio", "Display", "Graphics" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            int index = i;
            var tab = CreateButton(tabNames[i], tabs, tabNames[i], () => SelectTab(index));
            Anchor(tab.rectTransform, new Rect(i / 3f + 0.01f, 0f, 1f / 3f - 0.02f, 1f));
            tabImages.Add(tab);

            var page = CreateRect(tabNames[i] + "Page", content).gameObject;
            Stretch((RectTransform)page.transform);
            pages.Add(page);
        }

        // Audio
        var audio = pages[0].transform;
        AddSlider(audio, 0, "Master", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        AddSlider(audio, 1, "Music", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
        AddSlider(audio, 2, "Effects", () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);

        // Display
        var display = pages[1].transform;
        AddChoice(display, 0, "Resolution", () => GameSettings.Resolutions.Count, () => GameSettings.ResolutionIndex, i => GameSettings.ResolutionIndex = i, GameSettings.ResolutionName);
        AddChoice(display, 1, "Window Mode", () => GameSettings.WindowModeNames.Length, () => GameSettings.WindowMode, i => GameSettings.WindowMode = i, i => GameSettings.WindowModeNames[i]);
        AddChoice(display, 2, "V-Sync", () => 2, () => GameSettings.VSync, i => GameSettings.VSync = i, i => GameSettings.OnOffNames[i]);
        AddChoice(display, 3, "Frame Limit", () => GameSettings.FrameLimits.Length, () => GameSettings.FrameLimit, i => GameSettings.FrameLimit = i,
            i => GameSettings.VSync == 1 ? "V-Sync" : GameSettings.FrameLimits[i] < 0 ? "Unlimited" : GameSettings.FrameLimits[i] + " FPS");

        // Graphics
        var graphics = pages[2].transform;
        AddChoice(graphics, 0, "Shadows", () => GameSettings.ShadowNames.Length, () => GameSettings.Shadows, i => GameSettings.Shadows = i, i => GameSettings.ShadowNames[i]);
        AddChoice(graphics, 1, "Anti-Aliasing", () => GameSettings.MsaaNames.Length, () => GameSettings.AntiAliasing, i => GameSettings.AntiAliasing = i, i => GameSettings.MsaaNames[i]);
        AddChoice(graphics, 2, "Ambient Occlusion", () => 2, () => GameSettings.AmbientOcclusion, i => GameSettings.AmbientOcclusion = i, i => GameSettings.OnOffNames[i]);
        AddChoice(graphics, 3, "Render Scale", () => GameSettings.RenderScales.Length, () => GameSettings.RenderScale, i => GameSettings.RenderScale = i,
            i => Mathf.RoundToInt(GameSettings.RenderScales[i] * 100f) + "%");

        var reset = CreateButton("ResetButton", board.transform, "Defaults", GameSettings.ResetToDefaults);
        Anchor(reset.rectTransform, ResetButtonArea);
        var back = CreateButton("BackButton", board.transform, "Back", Close);
        Anchor(back.rectTransform, BackButtonArea);
    }

    private RectTransform CreateRow(Transform page, int index, string label, out RectTransform control)
    {
        var row = CreateRect(label + "Row", page);
        float h = 1f / RowsPerPage;
        Anchor(row, new Rect(0f, 1f - (index + 1) * h, 1f, h));

        var text = CreateText("Label", row, label, 30, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Anchor(text.rectTransform, new Rect(0f, 0.1f, 0.32f, 0.8f));

        control = CreateRect("Control", row);
        Anchor(control, new Rect(0.34f, 0.04f, 0.66f, 0.92f));
        return row;
    }

    private void AddSlider(Transform page, int index, string label, Func<float> get, Action<float> set)
    {
        CreateRow(page, index, label, out var control);

        // Plate keeps the art's aspect, left-aligned in the control area and slightly taller than the row
        // (its border is mostly empty space); the value sits right next to it.
        var plate = CreateImage("Plate", control, sliderPlate, Color.white);
        plate.rectTransform.anchorMin = new Vector2(0f, -0.04f);
        plate.rectTransform.anchorMax = new Vector2(0f, 1.04f);
        plate.rectTransform.pivot = new Vector2(0f, 0.5f);
        plate.rectTransform.offsetMin = plate.rectTransform.offsetMax = Vector2.zero;
        var fitter = plate.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        fitter.aspectRatio = PlateAspect;

        var bar = CreateRect("Bar", plate.transform);
        Anchor(bar, PlateBarArea);
        var track = CreateImage("Track", bar, sliderTrack, Color.white);
        track.type = Image.Type.Sliced;
        Stretch(track.rectTransform);
        track.gameObject.AddComponent<FitSpriteToHeight>();

        var fillArea = CreateRect("FillArea", bar);
        Stretch(fillArea);
        var fill = CreateImage("Fill", fillArea, sliderFill, Color.white);
        fill.type = Image.Type.Tiled;
        Stretch(fill.rectTransform);
        fill.gameObject.AddComponent<FitSpriteToHeight>();

        var handleArea = CreateRect("HandleArea", bar);
        Stretch(handleArea);
        var handle = CreateImage("Handle", handleArea, sliderHandle, Color.white);
        // Lives on the bar: it resizes the handle area, so it must not listen to that area's own size changes.
        var sizer = bar.gameObject.AddComponent<HandleSizer>();
        sizer.handleArea = handleArea;
        sizer.handle = handle.rectTransform;

        var slider = bar.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        var value = CreateText("Value", plate.transform, "", 28, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        value.rectTransform.anchorMin = new Vector2(1f, 0.25f);
        value.rectTransform.anchorMax = new Vector2(1f, 0.75f);
        value.rectTransform.pivot = new Vector2(0f, 0.5f);
        value.rectTransform.offsetMin = new Vector2(16f, 0f);
        value.rectTransform.offsetMax = new Vector2(116f, 0f);

        slider.onValueChanged.AddListener(v =>
        {
            value.text = Mathf.RoundToInt(v * 100f) + "%";
            if (!Mathf.Approximately(v, get())) set(v);
        });
        refreshers.Add(() =>
        {
            slider.SetValueWithoutNotify(get());
            value.text = Mathf.RoundToInt(get() * 100f) + "%";
        });
    }

    private void AddChoice(Transform page, int index, string label, Func<int> count, Func<int> get, Action<int> set, Func<int, string> name)
    {
        CreateRow(page, index, label, out var control);

        var value = CreateText("Value", control, "", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(value.rectTransform, new Rect(0.14f, 0.05f, 0.72f, 0.9f));

        void Step(int delta)
        {
            int n = count();
            if (n <= 0) return;
            set((get() + delta + n) % n);
        }
        var left = CreateArrow("Previous", control, false, () => Step(-1));
        Anchor(left.rectTransform, new Rect(0f, 0.1f, 0.12f, 0.8f));
        var right = CreateArrow("Next", control, true, () => Step(1));
        Anchor(right.rectTransform, new Rect(0.88f, 0.1f, 0.12f, 0.8f));

        refreshers.Add(() => value.text = name(get()));
    }

    private Image CreateArrow(string objectName, Transform parent, bool pointsRight, UnityAction onClick)
    {
        if (arrowSprite == null) return CreateButton(objectName, parent, pointsRight ? ">" : "<", onClick);

        var image = CreateImage(objectName, parent, arrowSprite, Color.white);
        image.preserveAspect = true;
        // The art points left; the right arrow is the same sprite mirrored.
        if (pointsRight) image.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        var button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        image.gameObject.AddComponent<ButtonHoverScale>();
        return image;
    }

    private Image CreateButton(string objectName, Transform parent, string label, UnityAction onClick)
    {
        var image = CreateImage(objectName, parent, buttonSprite, buttonSprite != null ? Color.white : new Color(0.93f, 0.6f, 0.2f, 1f));
        var button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        image.gameObject.AddComponent<ButtonHoverScale>();
        var text = CreateText("Label", image.transform, label, 28, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(text.rectTransform, new Rect(0.08f, 0.12f, 0.84f, 0.76f));
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string content, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = textColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.5f;
        text.fontSizeMax = size;
        return text;
    }

    private static void Anchor(RectTransform rt, Rect area)
    {
        rt.anchorMin = area.min;
        rt.anchorMax = area.max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rt) => Anchor(rt, new Rect(0f, 0f, 1f, 1f));

    #endregion

    #region Slider Helpers

    // Scales sliced/tiled bar sprites so their caps and stripes match the bar height at any resolution.
    private class FitSpriteToHeight : MonoBehaviour
    {
        private void OnRectTransformDimensionsChange() => Fit();
        private void Start() => Fit();

        private void Fit()
        {
            var image = GetComponent<Image>();
            var parent = transform.parent as RectTransform;
            if (image == null || image.sprite == null || parent == null || parent.rect.height <= 0f) return;
            image.pixelsPerUnitMultiplier = image.sprite.rect.height / parent.rect.height;
        }
    }

    // Keeps the handle a bit taller than the bar (like the art) and lets its centre reach both ends.
    private class HandleSizer : MonoBehaviour
    {
        public RectTransform handleArea;
        public RectTransform handle;
        private const float HeightScale = 224f / 138f;
        private const float Aspect = 214f / 224f;
        private bool fitting;

        private void OnRectTransformDimensionsChange() => Fit();
        private void Start() => Fit();

        private void Fit()
        {
            if (fitting || handleArea == null || handle == null) return;
            fitting = true;
            float barHeight = ((RectTransform)transform).rect.height;
            float height = barHeight * HeightScale;
            float width = height * Aspect;
            handleArea.offsetMin = new Vector2(width * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-width * 0.5f, 0f);
            handle.sizeDelta = new Vector2(width, height - barHeight);
            fitting = false;
        }
    }

    #endregion
}
