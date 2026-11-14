using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Animated on-screen warnings for blocked player actions. Creates its own overlay canvas the first time a warning
// is shown in a scene, so levels don't need any setup. Singleplayer only: a host must not see other players' warnings.
public class WarningManager : MonoBehaviour
{
    public static WarningManager Instance { get; private set; }

    // Set by the player while the input from a successful interaction is still held: what follows is not a player mistake.
    public static bool IsMuted { get; set; }

    [Header("Layout")]
    [SerializeField] private Vector2 screenPosition = new Vector2(0.5f, 0.72f);
    [SerializeField] private Vector2 textSize = new Vector2(1000f, 120f);
    [SerializeField] private float fontSize = 46f;
    [SerializeField] private float outlineWidth = 0.2f;

    [Header("Colors")]
    [SerializeField] private Color appearColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Header("Scale")]
    [SerializeField] private float startScale = 0.6f;
    [SerializeField] private float peakScale = 1.2f;
    [SerializeField] private float endScale = 0.7f;

    [Header("Timing")]
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private float emphasizeDuration = 0.3f;
    [SerializeField] private float holdDuration = 0.8f;
    [SerializeField] private float fadeDuration = 0.5f;

    private TextMeshProUGUI warningText;
    private Coroutine warningRoutine;
    private string currentMessage;
    private float holdRemaining;
    private bool isFading;

    // Shows the warning for a blocked action.
    public static void Show(string message, bool ignoreMute = false)
    {
        if (NetworkModeHelper.IsNetworked) return;
        if (IsMuted && !ignoreMute) return;

        if (Instance == null) CreateInstance();
        SfxBootstrap.PlayWarning();
        Instance.Play(message);
    }
    // For interactions: warns and returns false, so a receiver can write "return WarningManager.Reject(...)".
    public static bool Reject(string message, bool ignoreMute = false)
    {
        Show(message, ignoreMute);
        return false;
    }
    // Multiplayer: local-only call sites (own player movement/use) use this; the gate above stays
    // for the SP station paths that could otherwise run on a server for a remote player.
    public static void ShowLocal(string message, bool ignoreMute = false)
    {
        if (IsMuted && !ignoreMute) return;

        if (Instance == null) CreateInstance();
        SfxBootstrap.PlayWarning();
        Instance.Play(message);
    }
    private static void CreateInstance()
    {
        GameObject root = new GameObject("WarningManager");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        root.AddComponent<WarningManager>().BuildText();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    private void BuildText()
    {
        GameObject textObject = new GameObject("WarningText", typeof(RectTransform));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = screenPosition;
        rect.anchorMax = screenPosition;
        rect.sizeDelta = textSize;
        rect.anchoredPosition = Vector2.zero;

        warningText = textObject.AddComponent<TextMeshProUGUI>();
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.fontStyle = FontStyles.Bold;
        warningText.fontSize = fontSize;
        warningText.raycastTarget = false;
        warningText.outlineWidth = outlineWidth;
        warningText.outlineColor = Color.black;
        warningText.alpha = 0f;
    }
    private void Play(string message)
    {
        // Holding the input repeats the same warning: keep it up instead of restarting the animation.
        if (warningRoutine != null && message == currentMessage && !isFading)
        {
            holdRemaining = holdDuration;
            return;
        }

        if (warningRoutine != null) StopCoroutine(warningRoutine);

        currentMessage = message;
        warningText.text = message;
        warningRoutine = StartCoroutine(WarningRoutine());
    }
    // Unscaled time, so a warning still finishes if the game gets paused.
    private IEnumerator WarningRoutine()
    {
        isFading = false;
        Transform target = warningText.transform;

        yield return Animate(appearDuration, t =>
        {
            warningText.color = WithAlpha(appearColor, t);
            target.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, t);
        });

        yield return Animate(emphasizeDuration, t =>
        {
            warningText.color = Color.Lerp(appearColor, warningColor, t);
            target.localScale = Vector3.one * Mathf.Lerp(1f, peakScale, t);
        });

        holdRemaining = holdDuration;
        while (holdRemaining > 0f)
        {
            holdRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        isFading = true;
        yield return Animate(fadeDuration, t =>
        {
            warningText.color = WithAlpha(warningColor, 1f - t);
            target.localScale = Vector3.one * Mathf.Lerp(peakScale, endScale, t);
        });

        warningRoutine = null;
    }
    private static IEnumerator Animate(float duration, System.Action<float> apply)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            apply(Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        apply(1f);
    }
    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
