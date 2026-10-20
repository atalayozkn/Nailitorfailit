using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Trial HUD layout: framed cards for gold (top-left) and time (top-center), and a short
// horizontal stamina bar in a card at the bottom-center. Styled after the dog UI card
// (orange frame, cream fill, dark brown ink). Safe to run more than once.
public static class HudLayoutTool
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player_Package_Default.prefab";
    private const string InnerName = "HUD_Inner";

    // Palette picked from the dog UI card.
    private static readonly Color FrameColor = Hex("F2A33A");
    private static readonly Color FillColor = Hex("FDE6B6");
    private static readonly Color InkColor = Hex("4A2E1A");
    private static readonly Color CoinColor = Hex("FFC531");
    private static readonly Color TrackColor = Hex("6B4A2E");
    private static readonly Color StaminaColor = Hex("6CD13E");

    // Layout (reference resolution 1920x1080).
    private const float ScreenMargin = 28f;
    private const float FrameWidth = 6f;

    [MenuItem("Tools/HUD/Apply Trial Layout To Player Prefab")]
    private static void ApplyToPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (Apply(root)) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // For scenes with an unpacked copy of the player (e.g. TutorialScene): select the player or its canvas first.
    [MenuItem("Tools/HUD/Apply Trial Layout To Selection")]
    private static void ApplyToSelection()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogWarning("HudLayoutTool: Select the player (or its OverlayCanvas) first.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Apply Trial HUD Layout");
        if (Apply(root)) EditorSceneManager.MarkSceneDirty(root.scene);
    }

    private static bool Apply(GameObject root)
    {
        Canvas canvas = Find(root.transform, "OverlayCanvas")?.GetComponent<Canvas>();
        RectTransform currency = Find(root.transform, "Currency") as RectTransform;
        RectTransform timer = Find(root.transform, "TimeCounter") as RectTransform;
        Slider stamina = Find(root.transform, "Stamina_Slider")?.GetComponent<Slider>();

        if (canvas == null || currency == null || timer == null || stamina == null)
        {
            Debug.LogError("HudLayoutTool: Could not find OverlayCanvas / Currency / TimeCounter / Stamina_Slider under " + root.name);
            return false;
        }

        SetupCanvasScaler(canvas);
        LayoutCurrency(currency);
        LayoutTimer(timer);
        LayoutStamina(stamina);

        Debug.Log("HudLayoutTool: Trial HUD layout applied to " + root.name);
        return true;
    }

    #region Elements

    // Scale with resolution; at 1920x1080 everything that exists today stays where it is.
    private static void SetupCanvasScaler(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void LayoutCurrency(RectTransform card)
    {
        ResetTransform(card);
        Place(card, new Vector2(0f, 1f), new Vector2(ScreenMargin, -ScreenMargin), new Vector2(210f, 78f));
        MakeCard(card);

        // Coin icon
        RectTransform coin = GetOrCreateChild(card, "HUD_CoinIcon");
        coin.SetSiblingIndex(1);
        coin.anchorMin = coin.anchorMax = new Vector2(0f, 0.5f);
        coin.pivot = new Vector2(0.5f, 0.5f);
        coin.anchoredPosition = new Vector2(44f, 0f);
        coin.sizeDelta = new Vector2(46f, 46f);
        Image coinImage = GetOrAdd<Image>(coin.gameObject);
        coinImage.sprite = BuiltinSprite("UI/Skin/Knob.psd");
        coinImage.color = CoinColor;
        coinImage.raycastTarget = false;
        AddOutline(coin.gameObject, 3f);

        TextMeshProUGUI text = card.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            RectTransform t = text.rectTransform;
            ResetTransform(t);
            Stretch(t, new Vector2(80f, 6f), new Vector2(-18f, -6f));
            StyleText(text, 50f, TextAlignmentOptions.MidlineLeft);
        }
    }

    private static void LayoutTimer(RectTransform card)
    {
        ResetTransform(card);
        Place(card, new Vector2(0.5f, 1f), new Vector2(0f, -ScreenMargin), new Vector2(220f, 78f));
        MakeCard(card);

        TextMeshProUGUI text = card.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            RectTransform t = text.rectTransform;
            ResetTransform(t);
            Stretch(t, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            StyleText(text, 52f, TextAlignmentOptions.Center);
        }
    }

    private static void LayoutStamina(Slider slider)
    {
        RectTransform card = (RectTransform)slider.transform;
        ResetTransform(card);
        Place(card, new Vector2(0.5f, 0f), new Vector2(0f, ScreenMargin), new Vector2(460f, 64f));
        MakeCard(card);

        // HUD bar, not an input: no handle, no hover tint.
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        if (slider.handleRect != null)
        {
            slider.handleRect.parent.gameObject.SetActive(false);
            slider.handleRect = null;
        }

        // Label
        RectTransform label = GetOrCreateChild(card, "HUD_StaminaLabel");
        label.anchorMin = new Vector2(0f, 0f);
        label.anchorMax = new Vector2(0f, 1f);
        label.pivot = new Vector2(0f, 0.5f);
        label.anchoredPosition = new Vector2(20f, 0f);
        label.sizeDelta = new Vector2(120f, 0f);
        TextMeshProUGUI labelText = GetOrAdd<TextMeshProUGUI>(label.gameObject);
        labelText.text = "STAMINA";
        TextMeshProUGUI anyText = FindAnyHudFont(card);
        if (anyText != null) labelText.font = anyText.font;
        StyleText(labelText, 24f, TextAlignmentOptions.MidlineLeft);

        // Track (the slider's background image)
        Vector2 trackMin = new Vector2(146f, 20f);
        Vector2 trackMax = new Vector2(-20f, -20f);
        Transform background = card.Find("Background (1)") ?? card.Find("Background");
        if (background != null)
        {
            RectTransform bg = (RectTransform)background;
            ResetTransform(bg);
            Stretch(bg, trackMin, trackMax);
            Image bgImage = bg.GetComponent<Image>();
            if (bgImage != null) StyleSliced(bgImage, TrackColor);
        }

        // Fill, inset a little inside the track
        if (slider.fillRect != null)
        {
            RectTransform fillArea = (RectTransform)slider.fillRect.parent;
            ResetTransform(fillArea);
            Stretch(fillArea, trackMin + new Vector2(4f, 4f), trackMax - new Vector2(4f, 4f));

            RectTransform fill = slider.fillRect;
            ResetTransform(fill);
            fill.sizeDelta = Vector2.zero;
            fill.anchoredPosition = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            if (fillImage != null) StyleSliced(fillImage, StaminaColor);
        }
    }

    #endregion

    #region Helpers

    // Orange frame (image on the card itself, dark outline) with a cream inner panel behind the content.
    private static void MakeCard(RectTransform card)
    {
        Image frame = GetOrAdd<Image>(card.gameObject);
        StyleSliced(frame, FrameColor);
        frame.raycastTarget = false;
        AddOutline(card.gameObject, 3f);

        RectTransform inner = GetOrCreateChild(card, InnerName);
        inner.SetSiblingIndex(0);
        Stretch(inner, new Vector2(FrameWidth, FrameWidth), new Vector2(-FrameWidth, -FrameWidth));
        Image innerImage = GetOrAdd<Image>(inner.gameObject);
        StyleSliced(innerImage, FillColor);
        innerImage.raycastTarget = false;
    }

    private static void StyleSliced(Image image, Color color)
    {
        image.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.6f; // rounder corners
        image.color = color;
        image.enabled = true;
    }

    private static void StyleText(TextMeshProUGUI text, float size, TextAlignmentOptions alignment)
    {
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = InkColor;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static void AddOutline(GameObject go, float distance)
    {
        Outline outline = GetOrAdd<Outline>(go);
        outline.effectColor = InkColor;
        outline.effectDistance = new Vector2(distance, -distance);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static void ResetTransform(RectTransform rt)
    {
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }

    private static RectTransform GetOrCreateChild(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static TextMeshProUGUI FindAnyHudFont(Transform from)
    {
        Canvas canvas = from.GetComponentInParent<Canvas>();
        foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.font != null && text.gameObject.name != "HUD_StaminaLabel") return text;
        }
        return null;
    }

    private static Transform Find(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Sprite BuiltinSprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    #endregion
}
