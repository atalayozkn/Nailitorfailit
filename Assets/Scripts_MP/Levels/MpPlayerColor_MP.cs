using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MpPlayerColor_MP : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Backdrop of the colour / character panel (the menu's wooden-brown panel).")]
    [SerializeField] private Sprite panelSprite;

    private static MpPlayerColor_MP instance;

    public static readonly Color[] Palette =
    {
        new Color(0.98f, 0.71f, 0.40f),
        new Color(0.36f, 0.66f, 0.95f),
        new Color(0.40f, 0.85f, 0.45f),
        new Color(0.92f, 0.35f, 0.35f),
        new Color(0.76f, 0.48f, 0.95f),
        new Color(0.30f, 0.85f, 0.85f),
        new Color(0.95f, 0.85f, 0.30f),
        new Color(0.95f, 0.95f, 0.95f),
    };

    private CanvasGroup group;
    private readonly Image[] swatches = new Image[Palette.Length];
    private TMP_Text characterLabel;

    public static int Count => Palette.Length;

    public static int Clamp(int index) => index < 0 || index >= Palette.Length ? 0 : index;

    public static Color ColorOf(int index) => Palette[Clamp(index)];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpPlayerColor_MP>(FindObjectsInactive.Include) != null) return;

        var host = new GameObject("MpPlayerColor_MP");
        DontDestroyOnLoad(host);
        host.AddComponent<MpPlayerColor_MP>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying && gameObject.scene.IsValid()) DontDestroyOnLoad(gameObject);
        BindOrBuild();
    }

    // Prefab/scene-ekli olursa koddan kurulum yapmadan mevcut hiyerarsiyi bagla (Scene Editor gorunurlugu).
    private void BindOrBuild()
    {
        var canvasChild = transform.Find("Canvas");
        if (canvasChild == null)
        {
            Build();
            return;
        }

        group = canvasChild.GetComponent<CanvasGroup>();
        var panel = canvasChild.Find("ColorPanel");
        if (panel == null) return;

        for (int i = 0; i < swatches.Length; i++)
        {
            var sw = panel.Find("Swatch" + i);
            if (sw != null) swatches[i] = sw.GetComponent<Image>();
        }

        // The character picker is gone (one body for everybody): the panel only has the colour swatches.
        var caption = panel.Find("Caption");
        if (caption != null && caption.TryGetComponent(out TMP_Text captionText)) captionText.text = "COLOR";

        ApplyPanelLook(panel.GetComponent<Image>());

        // A scene saved by an older version may still carry the picker: remove it.
        foreach (string leftover in new[] { "CharacterCaption", "CharacterName", "CharacterPrev", "CharacterNext" })
        {
            var extra = panel.Find(leftover);
            if (extra != null) Destroy(extra.gameObject);
        }
    }

    // Opaque wooden panel (the same one behind the menu buttons) when the sprite is assigned in the scene.
    private void ApplyPanelLook(Image backdrop)
    {
        // The panel built in code (when the menu scene object was not the first instance) has no scene reference.
        if (panelSprite == null) panelSprite = Resources.Load<Sprite>("Menu/MenuButtons_Panel");
        if (backdrop == null || panelSprite == null) return;
        backdrop.sprite = panelSprite;
        backdrop.type = Image.Type.Sliced;
        backdrop.color = Color.white;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        LobbyNetworkManager manager = NetworkManager.singleton as LobbyNetworkManager;
        // IsGameStarted is only set on the host: clients kept the panel open inside the level.
        bool inLobby = manager != null && !manager.IsGameStarted && NetworkClient.localPlayer != null &&
                       UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == LobbyNetworkManager.MenuSceneName;

        if (group != null)
        {
            bool shouldShow = inLobby;
            group.alpha = Mathf.MoveTowards(group.alpha, shouldShow ? 1f : 0f, 6f * Time.unscaledDeltaTime);
            group.blocksRaycasts = shouldShow;
        }

        if (!inLobby) return;

        LobbyPlayer local = NetworkClient.localPlayer.GetComponent<LobbyPlayer>();
        if (local == null) return;

        if (characterLabel != null) characterLabel.text = MpCharacterSet_MP.NameOf(local.characterIndex).ToUpperInvariant();

        for (int i = 0; i < swatches.Length; i++)
        {
            if (swatches[i] == null) continue;

            bool selected = LobbyPlayer.ClampColor(local.colorIndex) == i;
            swatches[i].rectTransform.localScale = selected ? Vector3.one * 1.15f : Vector3.one;

            // Colours another player wears are greyed out and cannot be picked.
            bool takenByOther = IsTakenByOther(i, local);
            Color baseColor = Palette[i];
            swatches[i].color = takenByOther ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.18f) : baseColor;

            Button button = swatches[i].GetComponent<Button>();
            if (button != null) button.interactable = !takenByOther;
        }
    }

    private static bool IsTakenByOther(int index, LobbyPlayer local)
    {
        foreach (LobbyPlayer other in FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
        {
            if (other != null && other != local && LobbyPlayer.ClampColor(other.colorIndex) == index) return true;
        }

        return false;
    }

    public void Pick(int index)
    {
        LobbyPlayer local = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>()
            : null;

        if (local == null)
        {
            InteractionDebug.Log("[MP-COLOR] pick ignored: the local lobby player is not spawned yet.");
            return;
        }

        if (IsTakenByOther(Clamp(index), local)) return;

        local.CmdSetColor(index);
        InteractionDebug.Log($"[MP-COLOR] requested colour {index} ({Palette[Clamp(index)]}).");
    }

    // direction: +1 next, -1 previous (wraps around).
    public void StepCharacter(int direction)
    {
        LobbyPlayer local = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>()
            : null;

        if (local == null) return;

        int count = MpCharacterSet_MP.Count;
        int next = ((local.characterIndex + direction) % count + count) % count;
        local.CmdSetCharacter(next);
        InteractionDebug.Log($"[MP-COLOR] requested character {next} ({MpCharacterSet_MP.NameOf(next)}).");
    }

    private void Build()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 65;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        RectTransform root = (RectTransform)canvasGo.transform;

        RectTransform panel = NewRect(root, "ColorPanel");
        panel.sizeDelta = new Vector2(170f, 330f);
        panel.anchorMin = new Vector2(1f, 0.5f);
        panel.anchorMax = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(-115f, 0f);

        Image backdrop = panel.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.05f, 0.06f, 0.08f, 0.75f);
        backdrop.raycastTarget = false;
        ApplyPanelLook(backdrop);

        TMP_Text caption = NewText(panel, "Caption", new Vector2(0f, 138f), 20f, "COLOR");
        caption.color = new Color(0.98f, 0.71f, 0.40f, 1f);

        for (int i = 0; i < Palette.Length; i++)
        {
            int index = i;

            int column = i % 2;
            int row = i / 2;

            RectTransform cell = NewRect(panel, "Swatch" + i);
            cell.sizeDelta = new Vector2(46f, 46f);
            cell.anchoredPosition = new Vector2(column == 0 ? -33f : 33f, 96f - row * 62f);

            Image image = cell.gameObject.AddComponent<Image>();
            image.color = Palette[i];

            Button button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Pick(index));

            swatches[i] = image;
        }

    }

    // Character row under the colour swatches: CHARACTER / < NAME >.
    private void BuildCharacterRow(RectTransform panel)
    {
        TMP_Text characterCaption = NewText(panel, "CharacterCaption", new Vector2(0f, -80f), 20f, "CHARACTER");
        characterCaption.color = new Color(0.98f, 0.71f, 0.40f, 1f);

        characterLabel = NewText(panel, "CharacterName", new Vector2(0f, -132f), 17f, "MAN");
        ((RectTransform)characterLabel.transform).sizeDelta = new Vector2(80f, 40f);
        characterLabel.fontStyle = FontStyles.Bold;
        characterLabel.enableAutoSizing = true;
        characterLabel.fontSizeMin = 9f;
        characterLabel.fontSizeMax = 17f;

        NewArrow(panel, "CharacterPrev", new Vector2(-54f, -132f), "<", () => StepCharacter(-1));
        NewArrow(panel, "CharacterNext", new Vector2(54f, -132f), ">", () => StepCharacter(1));
    }

    private static void NewArrow(Transform parent, string name, Vector2 position, string glyph, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform cell = NewRect(parent, name);
        cell.sizeDelta = new Vector2(34f, 40f);
        cell.anchoredPosition = position;

        Image image = cell.gameObject.AddComponent<Image>();
        image.color = new Color(0.98f, 0.71f, 0.40f, 1f);

        Button button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TMP_Text text = NewText(cell, "Glyph", Vector2.zero, 24f, glyph);
        ((RectTransform)text.transform).sizeDelta = new Vector2(34f, 40f);
        text.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        text.fontStyle = FontStyles.Bold;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TMP_Text NewText(Transform parent, string name, Vector2 position, float size, string text)
    {
        RectTransform rect = NewRect(parent, name);
        rect.sizeDelta = new Vector2(520f, 40f);
        rect.anchoredPosition = position;

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return label;
    }
}
