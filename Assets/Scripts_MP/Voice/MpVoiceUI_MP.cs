using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MpVoiceUI_MP : MonoBehaviour
{
    private const int MaxRows = 4;

    private static MpVoiceUI_MP instance;

    private CanvasGroup group;
    private TMP_Text statusText;
    private TMP_Text hintText;
    private readonly TMP_Text[] rows = new TMP_Text[MaxRows];
    private readonly Image[] rowDots = new Image[MaxRows];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (instance != null) return;
        if (FindAnyObjectByType<MpVoiceUI_MP>(FindObjectsInactive.Include) != null) return;

        var host = new GameObject("MpVoiceUI_MP");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<MpVoiceUI_MP>();
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

    // Sahneye elle eklenmis ise koddan kurulum yapmadan mevcut hiyerarsiyi bagla (Scene Editor gorunurlugu).
    private void BindOrBuild()
    {
        var canvasChild = transform.Find("Canvas");
        if (canvasChild == null)
        {
            Build();
            return;
        }

        group = canvasChild.GetComponent<CanvasGroup>();
        var panel = canvasChild.Find("VoicePanel");
        if (panel == null) return;

        statusText = panel.Find("Status") != null ? panel.Find("Status").GetComponent<TMP_Text>() : null;
        hintText = panel.Find("Hint") != null ? panel.Find("Hint").GetComponent<TMP_Text>() : null;

        for (int i = 0; i < MaxRows; i++)
        {
            var row = panel.Find("Speaker" + i);
            var dot = panel.Find("Dot" + i);
            if (row != null) rows[i] = row.GetComponent<TMP_Text>();
            if (dot != null) rowDots[i] = dot.GetComponent<Image>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (group == null) return;

        VoiceNetwork_MP voice = VoiceNetwork_MP.Instance;

        bool inMatch = NetworkClient.active && NetworkClient.localPlayer != null;
        bool show = inMatch && voice != null;

        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, 6f * Time.unscaledDeltaTime);

        if (!show) return;

        if (voice.IsTransmitting)
        {
            statusText.text = "MIKROFON: KONUSUYORSUN";
            statusText.color = new Color(0.40f, 0.85f, 0.45f, 1f);
        }
        else if (voice.Muted)
        {
            statusText.text = "MIKROFON: KAPALI";
            statusText.color = new Color(0.92f, 0.35f, 0.35f, 1f);
        }
        else
        {
            statusText.text = "MIKROFON: HAZIR";
            statusText.color = new Color(0.95f, 0.85f, 0.30f, 1f);
        }

        hintText.text = voice.SteamAvailable ? "V bas-konus  |  M sustur" : "Steam yok - ses kapali";

        FillRows();
    }

    private void FillRows()
    {
        int used = 0;

        foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
        {
            if (used >= MaxRows || identity == null) continue;
            if (identity == NetworkClient.localPlayer) continue;

            VoiceSpeaker_MP speaker = identity.GetComponent<VoiceSpeaker_MP>();
            if (speaker == null || !speaker.IsSpeaking) continue;

            rows[used].text = speaker.SpeakerName;
            rows[used].color = new Color(0.95f, 0.95f, 0.95f, 1f);
            rowDots[used].color = new Color(0.40f, 0.85f, 0.45f, 1f);

            used++;
        }

        for (int i = used; i < MaxRows; i++)
        {
            rows[i].text = string.Empty;
            rowDots[i].color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private void Build()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 68;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        RectTransform root = (RectTransform)canvasGo.transform;

        RectTransform panel = NewRect(root, "VoicePanel");
        panel.sizeDelta = new Vector2(330f, 150f);
        // Bottom-right: bottom-left is the dog panel (the mic status used to cover its lower bars).
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-190f, 105f);

        Image backdrop = panel.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.05f, 0.06f, 0.08f, 0.72f);
        backdrop.raycastTarget = false;

        statusText = NewText(panel, "Status", new Vector2(0f, 52f), 20f, "MIKROFON: HAZIR");
        hintText = NewText(panel, "Hint", new Vector2(0f, 26f), 16f, "V bas-konus  |  M sustur");
        hintText.color = new Color(0.75f, 0.75f, 0.75f, 1f);

        for (int i = 0; i < MaxRows; i++)
        {
            float y = -6f - i * 26f;

            RectTransform dot = NewRect(panel, "Dot" + i);
            dot.sizeDelta = new Vector2(12f, 12f);
            dot.anchorMin = new Vector2(0.5f, 0.5f);
            dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = new Vector2(-130f, y);

            Image image = dot.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            rowDots[i] = image;

            rows[i] = NewText(panel, "Speaker" + i, new Vector2(14f, y), 17f, string.Empty);
            rows[i].alignment = TextAlignmentOptions.Left;
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
        rect.sizeDelta = new Vector2(300f, 24f);
        rect.anchoredPosition = position;

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return label;
    }
}
