using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MpMenuUI : MonoBehaviour
{
    public static MpMenuUI Instance { get; private set; }

    [Header("Authored panels (wired by the extractor tool; empty = runtime menu)")]
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject createPanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private TMP_InputField joinPasswordInput;

    [Header("Lobby info")]
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text lobbyPasswordText;
    [SerializeField] private TMP_Text statusText;

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

            Debug.LogWarning("[MP-UI] a second MpMenuUI appeared; destroying only the duplicate component.");
            Destroy(this);
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

        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        if (nm == null) return;

        string info = $"Code: {nm.LobbyID}\nPassword: {nm.LobbyPassword}\n{nm.ReadySummary()}\n" +
                      $"Level: {MpLevelCatalog.Instance?.SelectedLabel()}";

        if (lobbyIdText != null) lobbyIdText.text = nm.LobbyID;
        if (lobbyPasswordText != null) lobbyPasswordText.text = nm.LobbyPassword;
        if (runtimeLobbyText != null) runtimeLobbyText.text = info;
    }

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

    private static bool leaveInProgress;

    public void OnLeavePressed()
    {
        if (leaveInProgress) return;
        leaveInProgress = true;

        try
        {
            LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
            if (nm == null)
            {
                Debug.LogWarning("[MP-UI] leave pressed but there is no LobbyNetworkManager.");
                CloseMenuUi();
                return;
            }

            bool wasHost = NetworkServer.active && NetworkClient.isConnected;
            Debug.Log($"[MP-UI] leave pressed (wasHost={wasHost} client={NetworkClient.active} server={NetworkServer.active})");

            SteamLobbyManager.Instance?.LeaveLobby();

            if (wasHost) nm.StopHost();
            else if (NetworkClient.active) nm.StopClient();
            else if (NetworkServer.active) nm.StopServer();

            nm.ResetLobbyState();
            CloseMenuUi();

            Debug.Log("[MP-UI] leave complete.");
        }
        finally
        {
            leaveInProgress = false;
        }
    }

    public void OnReadyPressed()
    {
        LobbyPlayer local = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>()
            : null;

        if (local == null)
        {
            Debug.LogWarning("[MP-UI] ready pressed but the local lobby player is not spawned yet.");
            return;
        }

        if (local.isHost)
        {
            Debug.Log("[MP-UI] the host does not mark itself ready.");
            return;
        }

        bool next = !local.isReady;
        local.CmdSetReady(next);
        Debug.Log($"[MP-UI] ready -> {next}");
    }

    private void CloseMenuUi()
    {
        MpInPlaceToggle inPlace = GetComponentInChildren<MpInPlaceToggle>(true);
        if (inPlace != null)
        {
            inPlace.Close();
            return;
        }

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

    private enum RuntimeView { Multiplayer, Create, Join, Lobby }
    private RuntimeView runtimeView = RuntimeView.Multiplayer;

    private GameObject runtimeMultiplayerRow;
    private GameObject runtimeCreateRow;
    private GameObject runtimeJoinRow;
    private GameObject runtimeLobbyRow;

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

        runtimeMultiplayerRow = CreateRow(root, "Row_MP", new Vector2(0f, -20f));
        CreateButton(runtimeMultiplayerRow.GetComponent<RectTransform>(), "Create", new Vector2(-160f, 0f), OnCreatePressed);
        CreateButton(runtimeMultiplayerRow.GetComponent<RectTransform>(), "Join", new Vector2(160f, 0f), OnJoinPressed);

        runtimeCreateRow = CreateRow(root, "Row_Create", new Vector2(0f, -20f));
        CreateButton(runtimeCreateRow.GetComponent<RectTransform>(), "Create lobby", new Vector2(-160f, 0f), OnCreatePressed);
        CreateButton(runtimeCreateRow.GetComponent<RectTransform>(), "Back", new Vector2(160f, 0f), OnBackPressed);

        runtimeJoinRow = CreateRow(root, "Row_Join", new Vector2(0f, -20f));
        CreateButton(runtimeJoinRow.GetComponent<RectTransform>(), "Join lobby", new Vector2(-160f, 0f), OnJoinPressed);
        CreateButton(runtimeJoinRow.GetComponent<RectTransform>(), "Back", new Vector2(160f, 0f), OnBackPressed);

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
