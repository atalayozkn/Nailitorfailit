
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateLobbyUI : MonoBehaviour
{
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] TMP_InputField maxPlayersInput;
    [SerializeField] Button createButton;
    [SerializeField] TMP_Text statusText;

    // "Player Count:  (2) (3) (4)": a label and three radio-style buttons replace the typed "Max Players" field.
    // Compete is always 2 (the others are locked).
    private sealed class Radio
    {
        public int value;
        public Button button;
        public Image plate;
        public TMP_Text label;
    }

    private static readonly Color RadioOn = new Color(0.98f, 0.71f, 0.40f, 1f);
    private static readonly Color RadioOff = new Color(0.30f, 0.20f, 0.12f, 1f);
    private static readonly Color RadioInkOn = new Color(0.20f, 0.12f, 0.06f, 1f);
    private static readonly Color RadioInkOff = new Color(0.85f, 0.75f, 0.58f, 1f);
    private static readonly Color Cream = new Color(1f, 0.93f, 0.78f, 1f);

    private readonly System.Collections.Generic.List<Radio> radios = new System.Collections.Generic.List<Radio>();
    private bool countBuilt;
    private int playerCount = LobbyNetworkManager.MaxPlayers;

    void Awake()
    {
        createButton.onClick.AddListener(OnCreate);
        BuildCountRadios();
    }

    private void BuildCountRadios()
    {
        if (maxPlayersInput == null) return;

        var slot = (RectTransform)maxPlayersInput.transform;
        var rootGo = new GameObject("PlayerCount", typeof(RectTransform));
        rootGo.layer = slot.gameObject.layer;
        var root = (RectTransform)rootGo.transform;
        root.SetParent(slot.parent, false);
        root.anchorMin = slot.anchorMin;
        root.anchorMax = slot.anchorMax;
        root.pivot = slot.pivot;
        root.anchoredPosition = slot.anchoredPosition;
        root.sizeDelta = slot.sizeDelta;
        root.localScale = slot.localScale;
        root.SetSiblingIndex(slot.GetSiblingIndex());

        TMP_Text template = createButton != null ? createButton.GetComponentInChildren<TMP_Text>(true) : null;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        labelGo.layer = rootGo.layer;
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(root, false);
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0.52f, 1f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (template != null) label.font = template.font;
        label.text = "Player Count:";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 30f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = Cream;
        label.raycastTarget = false;

        for (int i = 0; i < 3; i++)
        {
            var radio = new Radio { value = LobbyNetworkManager.MinPlayers + i };

            var go = new GameObject("Radio_" + radio.value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.layer = rootGo.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            float left = 0.55f + i * 0.15f;
            rect.anchorMin = new Vector2(left, 0.15f);
            rect.anchorMax = new Vector2(left + 0.13f, 0.85f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            radio.plate = go.GetComponent<Image>();
            radio.button = go.GetComponent<Button>();
            radio.button.targetGraphic = radio.plate;
            int chosen = radio.value;
            radio.button.onClick.AddListener(() => { playerCount = chosen; RefreshCount(); });

            var textGo = new GameObject("Number", typeof(RectTransform), typeof(CanvasRenderer));
            textGo.layer = rootGo.layer;
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            radio.label = textGo.AddComponent<TextMeshProUGUI>();
            if (template != null) radio.label.font = template.font;
            radio.label.text = radio.value.ToString();
            radio.label.alignment = TextAlignmentOptions.Center;
            radio.label.fontStyle = FontStyles.Bold;
            radio.label.enableAutoSizing = true;
            radio.label.fontSizeMin = 12f;
            radio.label.fontSizeMax = 32f;
            radio.label.raycastTarget = false;

            radios.Add(radio);
        }

        maxPlayersInput.gameObject.SetActive(false);
        countBuilt = true;
        RefreshCount();
    }

    private bool CompeteLobby => VersusMenuButton.VersusLobbyPending;

    private int ChosenPlayers => CompeteLobby ? 2 : Mathf.Clamp(playerCount, LobbyNetworkManager.MinPlayers, LobbyNetworkManager.MaxPlayers);

    private void RefreshCount()
    {
        if (!countBuilt) return;

        int chosen = ChosenPlayers;
        bool locked = CompeteLobby;

        foreach (Radio radio in radios)
        {
            bool on = radio.value == chosen;
            radio.plate.color = on ? RadioOn : RadioOff;
            radio.label.color = on ? RadioInkOn : RadioInkOff;
            radio.button.interactable = !locked;

            // Compete is always 2 players: the 3 and the 4 are not shown at all.
            bool shown = !locked || on;
            if (radio.button.gameObject.activeSelf != shown) radio.button.gameObject.SetActive(shown);
        }
    }

    void Update() => RefreshCount();

    void OnEnable()
    {
        if (createButton != null) createButton.interactable = true;
        if (statusText != null) statusText.text = string.Empty;
        LobbyNetworkManager.SetupFailed += OnSetupFailed;
    }

    void OnDisable() => LobbyNetworkManager.SetupFailed -= OnSetupFailed;

    // Hosting failed (offline, Steam down...): let the player try again and say why.
    void OnSetupFailed(string reason)
    {
        if (createButton != null) createButton.interactable = true;
        if (statusText != null) statusText.text = reason;
    }

    void OnCreate()
    {
        // No Steam / no internet: say so on screen right away, before anything else is checked.
        if (!LobbyNetworkManager.ForceDevTransport && !MpNotice_MP.CanUseSteamOnline(out string steamProblem))
        {
            MpNotice_MP.Error(steamProblem);
            if (statusText != null) statusText.text = string.Empty;
            return;
        }

        string pw = passwordInput.text;

        if (string.IsNullOrWhiteSpace(pw))
        {
            if (statusText != null) statusText.text = "Password required";
            return;
        }

        int maxPlayers = countBuilt ? ChosenPlayers : LobbyNetworkManager.MaxPlayers;
        if (!countBuilt && maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int typed))
            maxPlayers = Mathf.Clamp(typed, LobbyNetworkManager.MinPlayers, LobbyNetworkManager.MaxPlayers);

        NetworkManager.singleton.maxConnections = maxPlayers;

        createButton.interactable = false;
        if (statusText != null) statusText.text = "Creating lobby...";

        ((LobbyNetworkManager)NetworkManager.singleton).HostLobby(pw);
    }

    public void OnExit()
    {
        if (createButton != null) createButton.interactable = true;
        if (statusText != null) statusText.text = string.Empty;

        ((LobbyNetworkManager)NetworkManager.singleton).CancelPendingSetup();

        if (GameManager.Instance != null) GameManager.Instance.ShowMultiplayerMenu();
        else gameObject.SetActive(false);
    }
}
