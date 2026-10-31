using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Paged information card (level intros, dog explanation ...). Pauses the game while open.
// The UI is built in code, so it only needs a board sprite and works in every scene.
public class InfoPopup : MonoBehaviour
{
    public static InfoPopup Instance { get; private set; }

    [Header("Visuals")]
    [SerializeField] private Sprite boardSprite;
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color textColor = new Color(0.25f, 0.15f, 0.08f, 1f);

    private GameObject root;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI pageText;
    private TextMeshProUGUI buttonText;
    private TextMeshProUGUI hintText;
    private string pendingHint = "";
    private Image pageImage;

    private InfoPopupData current;
    private int pageIndex;
    private Action onClosed;
    private bool pausedByPopup;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureUI();
        root.SetActive(false);
    }
    public void EnsureUI()
    {
        if (root == null) BuildUI();
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    private void Update()
    {
        if (!IsOpen || Keyboard.current == null) return;
        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame) NextPage();
    }

    #region Show / Hide

    public void Show(InfoPopupData data, Action closed = null)
    {
        if (data == null || data.Pages == null || data.Pages.Length == 0) return;

        current = data;
        onClosed = closed;
        pageIndex = 0;
        root.SetActive(true);
        ShowPage();

        // Reuse the pause system so the timer, hazards and pause menu all respect the pop-up.
        if (GamePauseManager.Instance != null && !GamePauseManager.Instance.IsPaused)
        {
            GamePauseManager.Instance.SetPause(true);
            pausedByPopup = true;
        }
        FindFirstObjectByType<PlayerStateMachine>()?.OpenCursor();
    }

    // Small line at the bottom of the paper (e.g. how to reopen the pop-up). Empty hides it.
    public void SetHint(string text)
    {
        pendingHint = text ?? "";
        if (hintText == null) return;
        hintText.text = pendingHint;
        hintText.gameObject.SetActive(pendingHint.Length > 0);
    }

    // Shows a page without pausing or touching the player (editor previews / screenshots).
    public void PreviewPage(InfoPopupData data, int index)
    {
        EnsureUI();
        current = data;
        pageIndex = Mathf.Clamp(index, 0, data.Pages.Length - 1);
        root.SetActive(true);
        ShowPage();
    }

    public void NextPage()
    {
        if (!IsOpen) return;
        if (pageIndex < current.Pages.Length - 1)
        {
            pageIndex++;
            ShowPage();
            return;
        }
        Close();
    }

    private void Close()
    {
        root.SetActive(false);

        if (pausedByPopup && GamePauseManager.Instance != null) GamePauseManager.Instance.SetPause(false);
        pausedByPopup = false;
        FindFirstObjectByType<PlayerStateMachine>()?.CloseCursor();

        var callback = onClosed;
        onClosed = null;
        current = null;
        callback?.Invoke();
    }

    private void ShowPage()
    {
        var page = current.Pages[pageIndex];
        titleText.text = page.title;
        bodyText.text = page.body;
        pageText.text = current.Pages.Length > 1 ? (pageIndex + 1) + " / " + current.Pages.Length : "";
        buttonText.text = pageIndex < current.Pages.Length - 1 ? "Next  >" : "Got it!";
        pageImage.sprite = page.image;
        pageImage.gameObject.SetActive(page.image != null);
        // Text takes the whole paper unless there is a picture next to it.
        Anchor(bodyText.rectTransform, page.image != null ? PaperBodyWithImageArea : PaperBodyArea);
    }

    #endregion

    #region UI Building

    // Regions of the board art (Art/UI/1/5.png) as fractions of the image, bottom-left origin.
    // Everything is anchored to these so the text always lands on the plank / paper / note, at any resolution.
    private static readonly Rect PlankTitleArea = new Rect(0.42f, 0.80f, 0.30f, 0.13f); // right of the helmet
    private static readonly Rect NoteArea = new Rect(0.86f, 0.77f, 0.11f, 0.13f);
    private static readonly Rect PaperBodyArea = new Rect(0.12f, 0.26f, 0.77f, 0.46f);
    private static readonly Rect PaperBodyWithImageArea = new Rect(0.12f, 0.26f, 0.51f, 0.46f);
    private static readonly Rect PaperImageArea = new Rect(0.65f, 0.28f, 0.24f, 0.42f);
    private static readonly Rect PaperHintArea = new Rect(0.12f, 0.13f, 0.50f, 0.08f);
    private static readonly Rect PaperButtonArea = new Rect(0.70f, 0.13f, 0.19f, 0.10f);
    private const float BoardAspect = 2048f / 1357f;
    private const float PlankTilt = 2.5f; // the plank art is slightly rotated

    private void BuildUI()
    {
        root = new GameObject("InfoPopupCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dim = CreateImage("Dim", root.transform, null, dimColor);
        Stretch(dim.rectTransform, Vector2.zero, Vector2.one);

        // The board rect has exactly the art's aspect ratio, so the fractional anchors match the picture.
        var board = CreateImage("Board", root.transform, boardSprite, boardSprite != null ? Color.white : new Color(0.96f, 0.88f, 0.74f, 1f));
        SetRect(board.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1320f, 1320f / BoardAspect), Vector2.zero);

        titleText = CreateText("Title", board.transform, 44, FontStyles.Bold, TextAlignmentOptions.Center);
        // One line on the plank: long titles shrink instead of wrapping.
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        AutoSize(titleText, 22, 44);
        Anchor(titleText.rectTransform, PlankTitleArea);
        titleText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, PlankTilt);

        pageText = CreateText("Page", board.transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(pageText.rectTransform, NoteArea);
        pageText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);

        bodyText = CreateText("Body", board.transform, 32, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        AutoSize(bodyText, 20, 32);
        Anchor(bodyText.rectTransform, PaperBodyArea);

        pageImage = CreateImage("Image", board.transform, null, Color.white);
        pageImage.preserveAspect = true;
        Anchor(pageImage.rectTransform, PaperImageArea);

        hintText = CreateText("Hint", board.transform, 22, FontStyles.Italic, TextAlignmentOptions.BottomLeft);
        AutoSize(hintText, 14, 22);
        Anchor(hintText.rectTransform, PaperHintArea);
        SetHint(pendingHint);

        var buttonImage = CreateImage("NextButton", board.transform, null, new Color(0.93f, 0.6f, 0.2f, 1f));
        Anchor(buttonImage.rectTransform, PaperButtonArea);
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.onClick.AddListener(NextPage);
        buttonText = CreateText("Label", buttonImage.transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        AutoSize(buttonText, 18, 30);
        buttonText.color = Color.white;
        Stretch(buttonText.rectTransform, Vector2.zero, Vector2.one);
    }

    private static void Anchor(RectTransform rt, Rect area)
    {
        rt.anchorMin = area.min;
        rt.anchorMax = area.max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void AutoSize(TextMeshProUGUI text, float min, float max)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
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

    private TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = textColor;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    #endregion
}
