using Mirror;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DebugConsoleMP : MonoBehaviour
{
    public static DebugConsoleMP Instance { get; private set; }

    public static KeyCode ToggleKey = KeyCode.BackQuote;

    public static readonly KeyCode[] ExtraToggleKeys = { KeyCode.F1 };

    const int MaxEntries = 400;
    const int MaxLineLength = 3000;
    const int MaxStackTraceLines = 4;

    struct Entry
    {
        public string Time;
        public string Type;
        public string Message;

        public string ToRichText()
        {
            string body = Message ?? "";
            if (body.Length > MaxLineLength)
                body = body.Substring(0, MaxLineLength) + " ...";
            body = body.Replace("\r", "").Replace("\n", "<br>   ");

            switch (Type)
            {
                case "WARN": return $"<color=#e6c96a>[{Time}] {TrimmedType}</color> {body}";
                case "ERRO": return $"<color=#ff7b72>[{Time}] {TrimmedType}</color> {body}";
                default: return $"[{Time}] {TrimmedType} {body}";
            }
        }

        public string ToPlainText()
        {
            string body = (Message ?? "").Replace("<br>", "\n    ");
            return $"[{Time}] {Type.TrimEnd()} {body}";
        }

        string TrimmedType => Type.TrimEnd();
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly Queue<Entry> pending = new Queue<Entry>();
    readonly object pendingLock = new object();
    bool capturingSuppressed;

    GameObject consoleRoot;
    TMP_Text logText;
    ContentSizeFitter logFitter;
    TMP_InputField commandField;
    ScrollRect scrollRect;
    TMP_Text statusHeader;

    bool visible;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoSpawn()
    {
        if (FindAnyObjectByType<DebugConsoleMP>(FindObjectsInactive.Include) != null) return;
        new GameObject("DebugConsole_MP").AddComponent<DebugConsoleMP>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
        Application.logMessageReceivedThreaded += OnLogMessageReceived;
        Debug.Log($"[Console] MP debug console ready [{InteractionDebug.Version}]. Press ` (backquote, next to 1) or F1 to toggle.");
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Application.logMessageReceivedThreaded -= OnLogMessageReceived;
        Instance = null;
    }

    void Update()
    {
        FlushCapturedEntries();

        if (ReadToggleKey())
        {
            Toggle();
        }
    }

    static bool ReadToggleKey()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.backquoteKey.wasPressedThisFrame) return true;
            if (keyboard.f1Key.wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (UnityEngine.Input.GetKeyDown(ToggleKey)) return true;

        foreach (KeyCode extra in ExtraToggleKeys)
            if (UnityEngine.Input.GetKeyDown(extra)) return true;
#endif
        return false;
    }

    public void Toggle()
    {
        SetVisible(!visible);
    }

    public void SetVisible(bool on)
    {
        visible = on;
        if (consoleRoot != null) consoleRoot.SetActive(on);
        if (on)
        {
            RefreshLogText();
            ScrollToBottom();
        }
    }

    void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (capturingSuppressed) return;

        string label;
        switch (type)
        {
            case LogType.Error:
            case LogType.Assert: label = "ERRO"; break;
            case LogType.Exception: label = "EXCP"; break;
            case LogType.Warning: label = "WARN"; break;
            default: label = "INFO"; break;
        }

        bool isError = label == "ERRO" || label == "EXCP";

        string message = isError ? condition + "\n" + TrimStackTrace(stackTrace) : condition;

        lock (pendingLock)
        {

            if (pending.Count < 512) pending.Enqueue(new Entry { Time = DateTime.Now.ToString("HH:mm:ss"), Type = label, Message = message });
        }
    }

    static string TrimStackTrace(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace)) return "";
        string[] lines = stackTrace.Split('\n');
        int take = Mathf.Min(MaxStackTraceLines, lines.Length);
        return string.Join("\n", lines, 0, take).TrimEnd();
    }

    void FlushCapturedEntries()
    {
        bool addedAny = false;

        while (true)
        {
            Entry e;
            lock (pendingLock)
            {
                if (pending.Count == 0) break;
                e = pending.Dequeue();
            }
            entries.Add(e);
            if (entries.Count > MaxEntries) entries.RemoveRange(0, entries.Count - MaxEntries);
            addedAny = true;
        }

        if (!addedAny) return;

        if (visible) RefreshLogText();
    }

    void BuildUi()
    {
        consoleRoot = new GameObject("DebugConsole_Root", typeof(RectTransform));
        consoleRoot.transform.SetParent(transform, false);

        Canvas canvas = consoleRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = consoleRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        consoleRoot.AddComponent<GraphicRaycaster>();

        RectTransform root = (RectTransform)consoleRoot.transform;
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 8f);
        root.sizeDelta = new Vector2(1400f, 480f);

        Image background = CreateImage(root, "Panel_BG", new Color(0f, 0f, 0f, 0.88f));
        Stretch(background.rectTransform);

        statusHeader = CreateText(root, "Status", Color.white, 22);
        RectTransform statusRect = statusHeader.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(12f, -4f);
        statusRect.sizeDelta = new Vector2(-24f, 30f);

        RectTransform scrollArea = CreateSub(root, "Scroll", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        scrollArea.anchorMin = new Vector2(0f, 0f);
        scrollArea.anchorMax = new Vector2(1f, 1f);
        scrollArea.offsetMin = new Vector2(12f, 46f);
        scrollArea.offsetMax = new Vector2(-12f, -38f);
        Image scrollBg = scrollArea.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(1f, 1f, 1f, 0.04f);
        scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = CreateSub(scrollArea, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();

        GameObject textGo = new GameObject("LogText", typeof(RectTransform));
        textGo.transform.SetParent(viewport, false);
        TextMeshProUGUI log = textGo.AddComponent<TextMeshProUGUI>();
        log.color = Color.white;
        log.fontSize = 19;
        log.textWrappingMode = TextWrappingModes.Normal;
        log.verticalAlignment = VerticalAlignmentOptions.Top;
        log.enableAutoSizing = false;
        log.margin = new Vector4(8f, 4f, 8f, 4f);

        RectTransform logRect = (RectTransform)textGo.transform;
        logRect.anchorMin = new Vector2(0f, 0f);
        logRect.anchorMax = new Vector2(1f, 1f);
        logRect.pivot = new Vector2(0f, 0f);
        logRect.offsetMin = new Vector2(0f, 0f);
        logRect.offsetMax = new Vector2(-8f, 0f);

        logFitter = textGo.AddComponent<ContentSizeFitter>();
        logFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        logFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        logText = log;

        scrollRect.content = logRect;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        RectTransform inputRoot = CreateSub(root, "CommandField", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 2f), new Vector2(-84f, 32f));
        Image fieldBg = inputRoot.gameObject.AddComponent<Image>();
        fieldBg.color = new Color(1f, 1f, 1f, 0.08f);

        RectTransform textArea = CreateSub(inputRoot, "Text Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        textArea.gameObject.AddComponent<RectMask2D>();

        GameObject valueGo = new GameObject("Text", typeof(RectTransform));
        valueGo.transform.SetParent(textArea, false);
        TextMeshProUGUI valueText = valueGo.AddComponent<TextMeshProUGUI>();
        valueText.color = Color.white;
        valueText.fontSize = 19;
        valueText.textWrappingMode = TextWrappingModes.NoWrap;
        valueText.verticalAlignment = VerticalAlignmentOptions.Middle;
        Stretch((RectTransform)valueGo.transform);

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(textArea, false);
        TextMeshProUGUI placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholderText.color = new Color(1f, 1f, 1f, 0.35f);
        placeholderText.fontSize = 19;
        placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
        placeholderText.verticalAlignment = VerticalAlignmentOptions.Middle;
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.text = "type 'help' and press Enter";
        Stretch((RectTransform)placeholderGo.transform);

        commandField = inputRoot.gameObject.AddComponent<TMP_InputField>();
        commandField.textComponent = valueText;
        commandField.placeholder = placeholderText;
        commandField.text = "";
        commandField.lineType = TMP_InputField.LineType.SingleLine;
        commandField.onSubmit.AddListener(_ =>
        {
            ExecuteCommand(commandField.text);
            commandField.text = "";
            commandField.ActivateInputField();
        });

        CreateButton(root, "Btn_Close", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -46f), new Vector2(72f, 30f), "CLOSE", Toggle);
        CreateButton(root, "Btn_Copy", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-96f, -46f), new Vector2(72f, 30f), "COPY",
            () => ExecuteCommand("copy"));

        consoleRoot.SetActive(false);
        visible = false;
    }

    static Image CreateImage(RectTransform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI CreateText(RectTransform parent, string name, Color color, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.color = color;
        text.fontSize = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        return text;
    }

    static RectTransform CreateSub(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void CreateButton(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta, string label, Action onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x == 1f ? 1f : 0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(() => onClick());

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(rect, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.color = Color.white;
        text.fontSize = 18;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.horizontalAlignment = HorizontalAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.text = label;
        Stretch((RectTransform)textGo.transform);
    }

    void ExecuteCommand(string raw)
    {
        string command = (raw ?? "").Trim().ToLowerInvariant();
        if (command.Length == 0) return;

        switch (command)
        {
            case "help":
                Emit("Commands:\n" +
                     "  help     - this list\n" +
                     "  info     - network and lobby state\n" +
                     "  ping     - Mirror round trip time (clients)\n" +
                     "  players  - connected connections (host only)\n" +
                     "  scene    - active scene\n" +
                     "  ui       - open canvases (duplicate HUD tespiti)\n" +
                     "  leave    - stop host/client and reload the menu scene\n" +
                     "  copy     - copy the whole log to the clipboard\n" +
                     "  clear    - wipe the log");
                break;

            case "info":
            {
                LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
                Emit("scene=" + SceneManager.GetActiveScene().path +
                     "\nserverActive=" + NetworkServer.active +
                     " clientActive=" + NetworkClient.active +
                     " clientConnected=" + NetworkClient.isConnected +
                     "\ntransport=" + (Transport.active == null ? "none" : Transport.active.GetType().Name) +
                     "\nlobby=" + (nm == null || string.IsNullOrEmpty(nm.LobbyID) ? "-" : nm.LobbyID) +
                     " passwordEntered=" + (!string.IsNullOrEmpty(nm != null ? nm.LobbyPassword : null)) +
                     "\nnetworkAddress=" + NetworkManager.singleton.networkAddress +
                     "\nsteamOnline=" + (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.SteamOnline) +
                     "");
                break;
            }

            case "ping":
                Emit(NetworkClient.isConnected
                    ? $"RTT={NetworkTime.rtt * 1000f:F0} ms  variance={NetworkTime.rttVariance * 1000f:F0} ms"
                    : "Not connected as client (the host has no remote RTT).");
                break;

            case "players":
                Emit(PlayersReport());
                break;

            case "scene":
                Emit("Active scene: " + SceneManager.GetActiveScene().path);
                break;

            case "ui":
                Emit(UiReport());
                break;

            case "leave":
                ExecuteLeave();
                break;

            case "clear":
                entries.Clear();
                RefreshLogText();
                break;

            case "copy":
                GUIUtility.systemCopyBuffer = BuildPlainText();
                Emit("Log copied to the clipboard (" + entries.Count + " entries).");
                break;

            default:
                Emit("Unknown command '" + command + "'. Type help.");
                break;
        }
    }

    static string UiReport()
    {
        var lines = new List<string>();
        var counts = new Dictionary<string, int>();

        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null) continue;

            string state = canvas.isActiveAndEnabled ? "on " : "off";
            lines.Add($"{state} {canvas.renderMode,-16} order={canvas.sortingOrder,4} {PathOf(canvas.transform)}");

            counts.TryGetValue(canvas.name, out int count);
            counts[canvas.name] = count + 1;
        }

        lines.Sort();

        var sb = new StringBuilder();
        sb.Append("Canvases (").Append(lines.Count).Append("):\n");
        foreach (string line in lines) sb.Append("  ").Append(line).Append('\n');

        sb.Append("Duplicate canvas names:\n");
        bool any = false;
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value < 2) continue;
            sb.Append("  ").Append(pair.Key).Append(" x").Append(pair.Value).Append('\n');
            any = true;
        }

        if (!any) sb.Append("  (none)\n");

        Debug.Log("[UI-REPORT] " + sb.ToString().Replace("\n", " | "));
        return sb.ToString();
    }

    static string PathOf(Transform transform)
    {
        var sb = new StringBuilder(transform.name);

        for (Transform node = transform.parent; node != null; node = node.parent)
            sb.Insert(0, node.name + "/");

        return sb.ToString();
    }

    static string PlayersReport()
    {
        if (!NetworkServer.active)
            return "'players' is only useful on the host machine.";

        var sb = new StringBuilder();
        sb.Append("Connections (host view): local + ").Append(NetworkServer.connections.Count).Append(" remote");
        foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            sb.Append("\n  conn #").Append(pair.Key).Append(": ").Append(pair.Value.address);

        return sb.ToString();
    }

    void ExecuteLeave()
    {
        if (NetworkServer.active)
        {
            SteamLobbyManager.Instance?.LeaveLobby();
            NetworkManager.singleton.StopHost();
            Emit("Host stopped. Loading the menu scene ...");
            SceneManager.LoadScene(LobbyNetworkManager.MenuSceneName);
        }
        else if (NetworkClient.active)
        {
            SteamLobbyManager.Instance?.LeaveLobby();
            NetworkManager.singleton.StopClient();
            Emit("Client stopped. Loading the menu scene ...");
            SceneManager.LoadScene(LobbyNetworkManager.MenuSceneName);
        }
        else
        {
            Emit("Not connected (no host, no client).");
        }
    }

    void RefreshLogText()
    {
        if (logText == null) return;

        if (statusHeader != null) statusHeader.text = BuildStatusLine();

        var sb = new StringBuilder();
        foreach (Entry entry in entries) sb.AppendLine(entry.ToRichText());
        logText.text = sb.ToString().TrimEnd();
        if (logFitter != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(logText.rectTransform);
            ScrollToBottom();
        }
    }

    void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
    }

    static string BuildStatusLine()
    {
        string state = "solo (menu)";
        if (NetworkServer.active && NetworkClient.isConnected) state = "HOST";
        else if (NetworkServer.active) state = "server only";
        else if (NetworkClient.isConnected) state = "CLIENT";
        else if (NetworkClient.active) state = "client connecting ...";

        string transport = Transport.active == null ? "none" : Transport.active.GetType().Name;

        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        string lobby = nm == null || string.IsNullOrEmpty(nm.LobbyID) ? "-" : nm.LobbyID;

        string rtt = NetworkClient.isConnected ? $" rtt={NetworkTime.rtt * 1000f:F0}ms" : "";
        string conns = NetworkServer.active ? $" conn={NetworkServer.connections.Count + 1}" : "";

        string pingTable = NetworkPingMonitor_MP.LatestTable;
        string pingSuffix = string.IsNullOrEmpty(pingTable) ? "" : $"\n{pingTable}";

        return $"<b>MP DEBUG (` or F1)  [{InteractionDebug.Version}]</b>   {state}   transport={transport}   lobby={lobby}" +
               $"{rtt}{conns}   scene={SceneManager.GetActiveScene().name}{pingSuffix}";
    }

    string BuildPlainText()
    {
        var sb = new StringBuilder();
        foreach (Entry entry in entries) sb.AppendLine(entry.ToPlainText());
        return sb.ToString();
    }

    void Emit(string message)
    {
        capturingSuppressed = true;
        Debug.Log("[Console] " + message);
        capturingSuppressed = false;

        entries.Add(new Entry { Time = DateTime.Now.ToString("HH:mm:ss"), Type = "INFO", Message = "[Console] " + message });
        RefreshLogText();
    }
}
