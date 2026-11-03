using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MpPlayerColor_MP : MonoBehaviour
{
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
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        LobbyNetworkManager manager = NetworkManager.singleton as LobbyNetworkManager;
        bool inLobby = manager != null && !manager.IsGameStarted && NetworkClient.localPlayer != null;

        if (group != null)
        {
            bool shouldShow = inLobby;
            group.alpha = Mathf.MoveTowards(group.alpha, shouldShow ? 1f : 0f, 6f * Time.unscaledDeltaTime);
            group.blocksRaycasts = shouldShow;
        }

        if (!inLobby) return;

        LobbyPlayer local = NetworkClient.localPlayer.GetComponent<LobbyPlayer>();
        if (local == null) return;

        for (int i = 0; i < swatches.Length; i++)
        {
            if (swatches[i] == null) continue;

            bool selected = LobbyPlayer.ClampColor(local.colorIndex) == i;
            swatches[i].rectTransform.localScale = selected ? Vector3.one * 1.15f : Vector3.one;
        }
    }

    private void Pick(int index)
    {
        LobbyPlayer local = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>()
            : null;

        if (local == null)
        {
            InteractionDebug.Log("[MP-COLOR] pick ignored: the local lobby player is not spawned yet.");
            return;
        }

        local.CmdSetColor(index);
        InteractionDebug.Log($"[MP-COLOR] requested colour {index} ({Palette[Clamp(index)]}).");
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

        TMP_Text caption = NewText(panel, "Caption", new Vector2(0f, 138f), 20f, "RENK");
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
