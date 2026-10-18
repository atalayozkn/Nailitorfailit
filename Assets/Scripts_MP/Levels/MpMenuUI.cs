using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The multiplayer menu front-end (M2): Create / Join / Lobby / Leave / Start plus the
/// player name and the level picker.
///
/// It talks ONLY to the public API of the MP stack
/// (LobbyNetworkManager.HostLobby / JoinLobby / StartGame / ReadySummary / LobbyID,
/// SteamLobbyManager.LeaveLobby, MpLevelNet for the level choice) - so the same
/// component drives the authored panels extracted from MenuScene_MP and, when those are
/// absent, a compact menu it builds itself at runtime.
///
/// That fallback is what makes the module usable from ANY menu scene immediately:
///    MpBootstrap.Open()  -> menu appears, no scene surgery required.
/// </summary>
public class MpMenuUI : MonoBehaviour
{
    public static MpMenuUI Instance { get; private set; }

    [Header("Authored panels (wired by the extractor tool; empty = runtime menu)")]
    [SerializeField] private GameObject multiplayerPanel;   // Buttons_AfterMultiplayer
    [SerializeField] private GameObject createPanel;        // Panel_AfterCreate
    [SerializeField] private GameObject joinPanel;          // Panel_AfterJoin
    [SerializeField] private GameObject lobbyPanel;         // Panel_Lobby

    [Header("Inputs")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private TMP_InputField joinPasswordInput;

    [Header("Lobby info")]
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text lobbyPasswordText;
    [SerializeField] private TMP_Text statusText;

    // runtime-built widgets
    private GameObject runtimeRoot;
    private TMP_InputField runtimeNameInput;
    private TMP_InputField runtimeCodeInput;
    private TMP_InputField runtimePasswordInput;
    private TMP_Text runtimeLobbyText;
    private bool runtimeMode;

    public bool UsingAuthoredPanels => !runtimeMode && (multiplayerPanel != null || lobbyPanel != null);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (nameInput != null) nameInput.text = PlayerPrefs.GetString("mp_player_name", string.Empty);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (statusText != null && NetworkClient.isConnected == false && NetworkServer.active == false)
            statusText.text = string.Empty;

        if (lobbyPanel == null && runtimeLobbyText == null) return;

        // live lobby info
        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        if (nm == null) return;

        string info = $"Code: {nm.LobbyID}\nPassword: {nm.LobbyPassword}\n{nm.ReadySummary()}\n" +
                      $"Level: {MpLevelCatalog.Instance?.SelectedLabel()}";

        if (lobbyIdText != null) lobbyIdText.text = nm.LobbyID;
        if (lobbyPasswordText != null) lobbyPasswordText.text = nm.LobbyPassword;
        if (runtimeLobbyText != null) runtimeLobbyText.text = info;
    }

    // ---------------------------------------------------------------- navigation

    public void ShowMultiplayerMenu()
    {
        SetPanels(multiplayerPanel);
        if (runtimeMode) SetRuntimeView(RuntimeView.Multiplayer);
    }

    public void ShowCreate()
    {
        SetPanels(createPanel);
        if (runtimeMode) SetRuntimeView(RuntimeView.Create);
    }

    public void ShowJoin()
    {
        SetPanels(joinPanel);
        if (runtimeMode) SetRuntimeView(RuntimeView.Join);
    }

    public void ShowLobby()
    {
        SetPanels(lobbyPanel);
        if (runtimeMode) SetRuntimeView(RuntimeView.Lobby);
    }

    public void HideAll()
    {
        SetPanels(null);
        if (runtimeRoot != null) runtimeRoot.SetActive(false);
    }

    private void SetPanels(GameObject active)
    {
        if (multiplayerPanel != null) multiplayerPanel.SetActive(multiplayerPanel == active);
        if (createPanel != null) createPanel.SetActive(createPanel == active);
        if (joinPanel != null) joinPanel.SetActive(joinPanel == active);
        if (lobbyPanel != null) lobbyPanel.SetActive(lobbyPanel == active);

        if (runtimeMode && runtimeRoot != null) runtimeRoot.SetActive(active != null);
        else if (!runtimeMode) SetCanvasVisible(active != null);
    }

    /// <summary>Shows/hides the canvas that hosts the authored panels.</summary>
    private void SetCanvasVisible(bool visible)
    {
        Transform node = multiplayerPanel != null ? multiplayerPanel.transform
            : lobbyPanel != null ? lobbyPanel.transform
            : createPanel != null ? createPanel.transform
            : joinPanel != null ? joinPanel.transform : null;

        if (node == null) return;

        Canvas canvas = node.GetComponentInParent<Canvas>(true);
        if (canvas != null) canvas.gameObject.SetActive(visible);
    }

    // ---------------------------------------------------------------- actions

    public void OnNameChanged(string value)
    {
        PlayerPrefs.SetString("mp_player_name", value ?? string.Empty);
        PlayerPrefs.Save();
        LobbyNetworkManager.SetDesiredName(value);
    }

    public void OnCreatePressed()
    {
        string password = RuntimeOrAuthored(createPasswordInput, 2);
        if (string.IsNullOrWhiteSpace(password)) { SetStatus("Password required"); return; }

        PushName();
        SetStatus("Creating lobby...");
        ((LobbyNetworkManager)NetworkManager.singleton).HostLobby(password);
    }

    public void OnJoinPressed()
    {
        string code = RuntimeOrAuthored(codeInput, 1);
        string password = RuntimeOrAuthored(joinPasswordInput, 1);

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Code and password required");
            return;
        }

        PushName();
        SetStatus("Connecting...");
        ((LobbyNetworkManager)NetworkManager.singleton).JoinLobby(code, password);
    }

    public void OnLeavePressed()
    {
        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        if (nm == null) { HideAll(); return; }

        bool wasHost = NetworkServer.active && NetworkClient.isConnected;
        SteamLobbyManager.Instance?.LeaveLobby();

        if (wasHost) NetworkManager.singleton.StopHost();
        else NetworkManager.singleton.StopClient();

        nm.ResetLobbyState();
        HideAll();
    }

    public void OnStartPressed()
    {
        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        if (nm == null) return;

        nm.StartGame();
    }

    public void OnBackPressed()
    {
        // from create/join back to the multiplayer menu; from there, hide everything
        if (createPanel != null && createPanel.activeSelf) { ShowMultiplayerMenu(); return; }
        if (joinPanel != null && joinPanel.activeSelf) { ShowMultiplayerMenu(); return; }

        if (runtimeMode && runtimeView != RuntimeView.Multiplayer)
        {
            ShowMultiplayerMenu();
            return;
        }

        HideAll();
    }

    private void PushName()
    {
        string name = RuntimeOrAuthored(nameInput, 0);
        if (!string.IsNullOrWhiteSpace(name)) OnNameChanged(name);
    }

    private string RuntimeOrAuthored(TMP_InputField authored, int runtimeField)
    {
        if (!runtimeMode) return authored != null ? authored.text : string.Empty;

        switch (runtimeField)
        {
            case 0: return runtimeNameInput != null ? runtimeNameInput.text : "";
            case 1: return runtimeCodeInput != null ? runtimeCodeInput.text : "";
            case 2: return runtimePasswordInput != null ? runtimePasswordInput.text : "";
            default: return "";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[MP-UI] {message}");
    }

    // ---------------------------------------------------------------- runtime menu

    private enum RuntimeView { Multiplayer, Create, Join, Lobby }
    private RuntimeView runtimeView = RuntimeView.Multiplayer;

    private GameObject runtimeMultiplayerRow;
    private GameObject runtimeCreateRow;
    private GameObject runtimeJoinRow;
    private GameObject runtimeLobbyRow;

    /// <summary>Builds a compact working menu in code (used when no authored prefab exists).</summary>
    public static MpMenuUI CreateRuntime()
    {
        var hostGo = new GameObject("MpMenuUI(runtime)");
        MpMenuUI ui = hostGo.AddComponent<MpMenuUI>();
        ui.runtimeMode = true;
        ui.BuildRuntimeCanvas();
        return ui;
    }

    private void BuildRuntimeCanvas()
    {
        runtimeRoot = new GameObject("MpMenuCanvas", typeof(RectTransform));
        runtimeRoot.transform.SetParent(transform, false);

        Canvas canvas = runtimeRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = runtimeRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        runtimeRoot.AddComponent<GraphicRaycaster>();

        RectTransform root = (RectTransform)runtimeRoot.transform;

        Image dim = CreateImage(root, "BG", new Color(0.05f, 0.06f, 0.08f, 0.96f));
        Stretch(dim.rectTransform);

        TMP_Text title = CreateText(root, "Title", new Vector2(0f, 330f), 56f, "MULTIPLAYER");
        title.color = new Color(1f, 0.92f, 0f, 1f);

        runtimeNameInput = CreateInput(root, "Name", new Vector2(0f, 240f), "player name");
        runtimeCodeInput = CreateInput(root, "Code", new Vector2(0f, 170f), "lobby code");
        runtimePasswordInput = CreateInput(root, "Password", new Vector2(0f, 100f), "password");

        // multiplayer (create / join / back)
        runtimeMultiplayerRow = CreateRow(root, "Row_MP", new Vector2(0f, -20f));
        CreateButton(runtimeMultiplayerRow.GetComponent<RectTransform>(), "Create", new Vector2(-160f, 0f), OnCreatePressed);
        CreateButton(runtimeMultiplayerRow.GetComponent<RectTransform>(), "Join", new Vector2(160f, 0f), OnJoinPressed);

        // create view
        runtimeCreateRow = CreateRow(root, "Row_Create", new Vector2(0f, -20f));
        CreateButton(runtimeCreateRow.GetComponent<RectTransform>(), "Create lobby", new Vector2(-160f, 0f), OnCreatePressed);
        CreateButton(runtimeCreateRow.GetComponent<RectTransform>(), "Back", new Vector2(160f, 0f), OnBackPressed);

        // join view
        runtimeJoinRow = CreateRow(root, "Row_Join", new Vector2(0f, -20f));
        CreateButton(runtimeJoinRow.GetComponent<RectTransform>(), "Join lobby", new Vector2(-160f, 0f), OnJoinPressed);
        CreateButton(runtimeJoinRow.GetComponent<RectTransform>(), "Back", new Vector2(160f, 0f), OnBackPressed);

        // lobby view
        runtimeLobbyRow = CreateRow(root, "Row_Lobby", new Vector2(0f, -20f));
        runtimeLobbyText = CreateText(root, "LobbyInfo", new Vector2(0f, 90f), 30f, string.Empty);

        CreateButton(runtimeLobbyRow.GetComponent<RectTransform>(), "Start", new Vector2(-240f, 0f), OnStartPressed);
        CreateButton(runtimeLobbyRow.GetComponent<RectTransform>(), "Leave", new Vector2(0f, 0f), OnLeavePressed);
        CreateButton(runtimeLobbyRow.GetComponent<RectTransform>(), "Level >>", new Vector2(240f, 0f),
            () => MpLevelNet.Instance?.HostNext());

        TMP_Text hint = CreateText(root, "Hint", new Vector2(0f, -280f), 26f, string.Empty);
        hint.text = "Host: Create -> pick a level -> Start.  Guest: type the code + password -> Join.";

        SetRuntimeView(RuntimeView.Multiplayer);
    }

    private void SetRuntimeView(RuntimeView view)
    {
        runtimeView = view;

        if (runtimeMultiplayerRow != null) runtimeMultiplayerRow.SetActive(view == RuntimeView.Multiplayer);
        if (runtimeCreateRow != null) runtimeCreateRow.SetActive(view == RuntimeView.Create);
        if (runtimeJoinRow != null) runtimeJoinRow.SetActive(view == RuntimeView.Join);
        if (runtimeLobbyRow != null) runtimeLobbyRow.SetActive(view == RuntimeView.Lobby);

        if (runtimeRoot != null) runtimeRoot.SetActive(true);
    }

    // ---------------------------------------------------------------- tiny builders

    private static Image CreateImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(RectTransform parent, string name, Vector2 position, float size, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = Color.white;

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 60f);
        rect.anchoredPosition = position;
        return tmp;
    }

    private static TMP_InputField CreateInput(RectTransform parent, string name, Vector2 position, string placeholder)
    {
        var go = new GameObject(name + "_Input", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 54f);
        rect.anchoredPosition = position;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.1f);

        var textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(rect, false);
        RectTransform areaRect = (RectTransform)textArea.transform;
        Stretch(areaRect);
        textArea.AddComponent<RectMask2D>();

        TMP_Text value = CreateText(areaRect, "Text", Vector2.zero, 30f, string.Empty);
        Stretch(value.rectTransform);

        TMP_Text hint = CreateText(areaRect, "Placeholder", Vector2.zero, 30f, placeholder);
        hint.color = new Color(1f, 1f, 1f, 0.35f);
        Stretch(hint.rectTransform);

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.textComponent = value as TextMeshProUGUI;
        field.placeholder = hint;
        field.lineType = TMP_InputField.LineType.SingleLine;
        return field;
    }

    private static GameObject CreateRow(RectTransform parent, string name, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 80f);
        rect.anchoredPosition = position;
        return go;
    }

    private static void CreateButton(RectTransform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Button_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, 64f);
        rect.anchoredPosition = position;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.22f, 0.22f, 0.26f, 0.95f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(onClick);

        TMP_Text text = CreateText(rect, "Label", Vector2.zero, 28f, label);
        text.color = new Color(1f, 0.92f, 0f, 1f);
        Stretch(text.rectTransform);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
