using Interactions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Small corner panel naming what the player is targeting and what the interact input would do right now.
// Built at runtime on the player's HUD. Singleplayer only, see PlayerInteractionHandler.Start.
public class LookAtInfoUI : MonoBehaviour
{
    private const string OverlayCanvasName = "OverlayCanvas";
    private const float RefreshInterval = 0.1f;
    // Keeps the panel from flickering when the target drops out for a moment at the edge of the reach.
    private const float HideDelay = 0.15f;

    // Offset from the screen centre, nudge it here if it sits on top of the player.
    private static readonly Vector2 PanelOffset = Vector2.zero;
    private const float PanelHeight = 84f;
    private const float PanelPadding = 18f;
    private const float NameFontSize = 28f;
    private const float ActionFontSize = 24f;
    private const float PulseDuration = 0.25f;
    private const float PulseScale = 1.15f;

    private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color NameColor = Color.white;
    private static readonly Color ActionColor = new Color(0.85f, 0.95f, 0.85f, 1f);
    private static readonly Color BlockedColor = new Color(1f, 0.72f, 0.3f, 1f);

    private PlayerInteractionHandler handler;
    private RectTransform panelRect;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI actionText;
    private float nextRefreshTime;
    private float lastSeenTime = float.NegativeInfinity;
    private bool isUsingGamepad;
    private bool isShowingBlocked;
    private float pulseElapsed = PulseDuration;

    // Multiplayer: injected look-at source (PlayerInteractionNetwork_MP). Null in singleplayer.
    private System.Func<LookAtInfo> mpSource;
    private System.Func<bool, string> mpBindingLabel;

    public static void Create(PlayerInteractionHandler handler)
    {
        Canvas overlayCanvas = FindOverlayCanvas(handler);
        if (overlayCanvas == null)
        {
            Debug.LogWarning($"LookAtInfoUI: no {OverlayCanvasName} found under the player, the look-at panel is disabled.");
            return;
        }

        GameObject panel = new GameObject("LookAtInfoPanel", typeof(RectTransform), typeof(CanvasGroup));
        panel.layer = overlayCanvas.gameObject.layer;
        panel.transform.SetParent(overlayCanvas.transform, false);
        panel.AddComponent<LookAtInfoUI>().Build(handler);
    }

    // Multiplayer: builds the panel from the MP interaction wrapper instead of the SP handler.
    public static void CreateMp(Transform playerRoot, System.Func<LookAtInfo> source, System.Func<bool, string> bindingLabel)
    {
        Canvas overlayCanvas = null;
        foreach (Canvas canvas in playerRoot.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.name == OverlayCanvasName)
            {
                overlayCanvas = canvas;
                break;
            }
        }
        if (overlayCanvas == null)
        {
            Debug.LogWarning($"LookAtInfoUI: no {OverlayCanvasName} found under {playerRoot.name}, the look-at panel is disabled.");
            return;
        }

        GameObject panel = new GameObject("LookAtInfoPanel", typeof(RectTransform), typeof(CanvasGroup));
        panel.layer = overlayCanvas.gameObject.layer;
        panel.transform.SetParent(overlayCanvas.transform, false);
        panel.AddComponent<LookAtInfoUI>().BuildMp(source, bindingLabel);
    }

    private static Canvas FindOverlayCanvas(PlayerInteractionHandler handler)
    {
        foreach (Canvas canvas in handler.transform.root.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.name == OverlayCanvasName) return canvas;
        }
        return null;
    }

    private void Build(PlayerInteractionHandler interactionHandler)
    {
        handler = interactionHandler;
        handler.InteractBlocked += OnInteractBlocked;
        InitLayout();
    }

    // Multiplayer: the MP interaction wrapper feeds the panel; no SP handler involved.
    private void BuildMp(System.Func<LookAtInfo> source, System.Func<bool, string> bindingLabel)
    {
        mpSource = source;
        mpBindingLabel = bindingLabel;
        InitLayout();
    }

    private void InitLayout()
    {
        panelRect = GetComponent<RectTransform>();
        // Screen centre so it is noticed; the bottom-right corner now holds the inventory bar.
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = PanelOffset;
        panelRect.sizeDelta = new Vector2(PanelHeight, PanelHeight);

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image background = gameObject.AddComponent<Image>();
        background.color = BackgroundColor;
        background.raycastTarget = false;

        nameText = CreateLine("Name", new Vector2(0.5f, 1f), NameFontSize, FontStyles.Bold, NameColor);
        actionText = CreateLine("Action", new Vector2(0.5f, 0.5f), ActionFontSize, FontStyles.Normal, ActionColor);
    }

    private TextMeshProUGUI CreateLine(string objectName, Vector2 anchorMax, float fontSize, FontStyles style, Color color)
    {
        GameObject line = new GameObject(objectName, typeof(RectTransform));
        line.layer = gameObject.layer;

        RectTransform rect = line.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0f, anchorMax.y - 0.5f);
        rect.anchorMax = new Vector2(1f, anchorMax.y);
        rect.offsetMin = new Vector2(PanelPadding, 0f);
        rect.offsetMax = new Vector2(-PanelPadding, 0f);

        TextMeshProUGUI text = line.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Midline;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void OnDestroy()
    {
        if (handler != null) handler.InteractBlocked -= OnInteractBlocked;
    }

    // The reason is already on the panel, this draws the eye to it.
    private void OnInteractBlocked()
    {
        if (isShowingBlocked && canvasGroup.alpha > 0f) pulseElapsed = 0f;
    }

    private void UpdatePulse()
    {
        if (pulseElapsed >= PulseDuration) return;

        pulseElapsed += Time.unscaledDeltaTime;
        float bump = Mathf.Sin(Mathf.Clamp01(pulseElapsed / PulseDuration) * Mathf.PI);
        panelRect.localScale = Vector3.one * Mathf.Lerp(1f, PulseScale, bump);
    }

    private void Update()
    {
        UpdateInputDevice();
        UpdatePulse();

        // The detection coroutine is frozen while paused, so this runs on unscaled time and hides itself.
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + RefreshInterval;

        LookAtInfo info = default;
        bool hasInfo;
        if (mpSource != null)
        {
            info = Time.timeScale > 0f ? mpSource() : default;
            hasInfo = !string.IsNullOrEmpty(info.Name);
        }
        else
        {
            hasInfo = !NetworkModeHelper.IsNetworked && Time.timeScale > 0f && handler != null && handler.TryGetLookAtInfo(out info);
        }

        if (hasInfo)
        {
            lastSeenTime = Time.unscaledTime;
            Show(info);
        }
        else if (Time.unscaledTime - lastSeenTime >= HideDelay)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void UpdateInputDevice()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && (gamepad.buttonEast.isPressed || gamepad.buttonSouth.isPressed || gamepad.leftStick.ReadValue().sqrMagnitude > 0.25f))
        {
            isUsingGamepad = true;
        }
        else if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
        {
            isUsingGamepad = false;
        }
    }

    private void Show(LookAtInfo info)
    {
        string binding = mpBindingLabel != null ? mpBindingLabel(isUsingGamepad) : handler.GetInteractBindingLabel(isUsingGamepad);
        string actionLine = info.IsBlocked ? info.Action : $"[{binding}] {info.Action}";
        Color actionColor = info.IsBlocked ? BlockedColor : ActionColor;

        if (nameText.text != info.Name || actionText.text != actionLine)
        {
            nameText.text = info.Name;
            actionText.text = actionLine;

            float width = Mathf.Max(nameText.GetPreferredValues(info.Name).x, actionText.GetPreferredValues(actionLine).x);
            panelRect.sizeDelta = new Vector2(width + PanelPadding * 2f, PanelHeight);
        }

        isShowingBlocked = info.IsBlocked;
        actionText.color = actionColor;
        canvasGroup.alpha = 1f;
    }
}
