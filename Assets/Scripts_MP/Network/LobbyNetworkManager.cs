
using System.Collections;
using System.Collections.Generic;
using kcp2k;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyNetworkManager : NetworkManager
{
    [Header("Lobby")]
    public GameObject gamePlayerPrefab;

    [Tooltip("Traffic car prefab (Props/Cars/Car_Active). CarController spawns it at runtime;\n" +
             "Mirror must know the asset on every machine, otherwise the spawned car\n" +
             "is refused with 'assets not registered'.")]
    [SerializeField] private GameObject trafficCarPrefab;

    public GameObject TrafficCarPrefab => trafficCarPrefab;

    public ulong SteamLobbyId { get; private set; }

    public string LobbyID { get; private set; }
    public string LobbyPassword { get; private set; }

    public static string DesiredName { get; private set; } = "";

    public static void SetDesiredName(string name)
    {
        DesiredName = name == null ? "" : name.Trim();
    }

    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;

    public const string GameScenePath = "Assets/Scenes_MP/DemoScene_MP.unity";

    public static string SelectedGameScenePath
    {
        get
        {
            string path = MpLevelCatalog.Instance != null ? MpLevelCatalog.Instance.SelectedScenePath : null;
            return string.IsNullOrEmpty(path) ? GameScenePath : path;
        }
    }

    public const string SpawnMarkerName = "SpawnPoint";

    public const string MenuSceneName = "MenuScene_MP";

    bool gameStarted = false;

    private int spawnSlot;

    bool connectedOnce = false;

    int setupGeneration;

    private bool returnToMenuRequested;

    private bool quitInProgress;

    public override void OnApplicationQuit()
    {
        quitInProgress = true;
        base.OnApplicationQuit();
    }

    // The transport assigned in the scene (FizzySteamworks). Dev mode swaps in KCP at runtime,
    // so Steam paths must put this back or they would silently keep using KCP.
    private Transport sceneTransport;

    public override void Awake()
    {
        base.Awake();

        // Duplicate (e.g. MenuScene_MP's manager when the MpStack from the SP menu already exists): Mirror
        // destroys it, but hand its spawn list to the survivor first. Otherwise the survivor only knows the
        // player + car, station products stay un-networked SP objects and clients reject "_MP" spawns.
        if (singleton != null && singleton != this)
        {
            foreach (GameObject prefab in spawnPrefabs)
            {
                if (prefab != null && !singleton.spawnPrefabs.Contains(prefab)) singleton.spawnPrefabs.Add(prefab);
            }
            return;
        }

        maxConnections = MaxPlayers; // also used as the Steam lobby size

        sceneTransport = transport;

        if (gamePlayerPrefab != null && !spawnPrefabs.Contains(gamePlayerPrefab))
            spawnPrefabs.Add(gamePlayerPrefab);

        if (trafficCarPrefab != null && !spawnPrefabs.Contains(trafficCarPrefab))
            spawnPrefabs.Add(trafficCarPrefab);
    }

    public override void Start()
    {
        base.Start();

        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnInviteCodeReady += HandleInviteCode;
    }

    public override void OnDestroy()
    {
        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnInviteCodeReady -= HandleInviteCode;

        base.OnDestroy();
    }

    private const string DevPrefKey = "NIOFI.MP.ForceDevTransport";

    private static bool forceDevTransport;

    public static bool ForceDevTransport
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(DevPrefKey, false); // Steam by default; F8 toggles local KCP
#else
            return forceDevTransport;
#endif
        }
        private set
        {
            forceDevTransport = value;

#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(DevPrefKey, value);
#endif
        }
    }

    private static bool LooksLikeAddress(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        string trimmed = input.Trim();

        return System.Net.IPAddress.TryParse(trimmed, out _) || trimmed.Equals("localhost",
            System.StringComparison.OrdinalIgnoreCase);
    }

    public override void Update()
    {
        base.Update();

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

        if (keyboard == null) return;
        if (!keyboard.f8Key.wasPressedThisFrame) return;

        ForceDevTransport = !ForceDevTransport;

        InteractionDebug.Log($"[DEV] forced local KCP transport {(ForceDevTransport ? "ON" : "OFF")} " +
                             "(use this for ParrelSync / two local instances).");
    }

    // Raised when hosting / joining fails before a connection exists, so the lobby screens can re-enable
    // their buttons and show the reason (the on-screen notice is shown by MpNotice_MP / SteamLobbyManager).
    public static event System.Action<string> SetupFailed;

    private static void ReportSetupFailure(string reason, bool showNotice)
    {
        Debug.LogWarning("[LOBBY] setup failed: " + reason);
        if (showNotice) MpNotice_MP.Error(reason);
        SetupFailed?.Invoke(reason);
    }

    public void HostLobby(string password)
    {
        LobbyPassword = password;

        UseSteamNameIfEmpty();

        ChatNetwork.ClearHistory();

        var auth = (LobbyAuthenticator)authenticator;
        auth.serverPassword = password;
        auth.clientPassword = password;
        auth.clientName = DesiredName;

        if (ForceDevTransport)
        {
            StartDevHost(password);
            return;
        }

        // No silent fallback to the local transport any more: a friend could never join that lobby.
        if (!MpNotice_MP.CanUseSteamOnline(out string offlineReason))
        {
            ReportSetupFailure(offlineReason, true);
            return;
        }

        var steam = SteamLobbyManager.Instance;

        if (steam == null)
        {
            Debug.LogError("[STEAM] SteamLobbyManager not found - could not create the lobby.");
            return;
        }

        UseSceneTransport();

        int gen = ++setupGeneration;
        steam.CreateLobby(
            maxConnections,
            onSuccess: code =>
            {
                if (gen != setupGeneration)
                {
                    Debug.Log("[LOBBY] Create cancelled by user - discarding the Steam lobby.");
                    steam.LeaveLobby();
                    return;
                }

                LobbyID = code;
                SteamLobbyId = steam.CurrentLobby.m_SteamID;

                Debug.Log($"[STEAM] Lobby ready. code={code} steamLobby={SteamLobbyId}");

                StartHost();
            },
            onFail: err => ReportSetupFailure(err, false));
    }

    private static void UseSteamNameIfEmpty()
    {
        if (!string.IsNullOrEmpty(DesiredName)) return;
        if (!SteamManager.Initialized) return;

        string persona = SteamFriends.GetPersonaName();

        if (string.IsNullOrEmpty(persona)) return;

        SetDesiredName(persona);
        InteractionDebug.Log($"[STEAM] lobby name taken from Steam: '{persona}'");
    }

    public void JoinLobby(string userInput, string password)
    {
        LobbyPassword = password;

        // Clients showed up as "Player 1" on the host: only HostLobby took the Steam name.
        UseSteamNameIfEmpty();

        ChatNetwork.ClearHistory();

        var auth = (LobbyAuthenticator)authenticator;
        auth.clientPassword = password;
        auth.clientName = DesiredName;

        bool devJoin = ForceDevTransport || LooksLikeAddress(userInput);

        if (devJoin)
        {
            StartDevClient(userInput, password);
            return;
        }

        if (!MpNotice_MP.CanUseSteamOnline(out string offlineReason))
        {
            ReportSetupFailure(offlineReason, true);
            return;
        }

        string input = LobbyCodeCodec.Canonicalize(userInput);

        var steam = SteamLobbyManager.Instance;

        int gen = ++setupGeneration;

        if (steam == null || !SteamManager.Initialized)
        {
            Debug.LogError("[STEAM] SteamLobbyManager not found - could not join.");
            return;
        }

        if (LobbyCodeCodec.IsValidShortCode(input))
        {
            steam.JoinLobbyByCode(input,
                onSuccess: () =>
                {
                    if (gen != setupGeneration)
                    {
                        Debug.Log("[LOBBY] Join cancelled by user - leaving the Steam lobby.");
                        steam.LeaveLobby();
                        return;
                    }
                    BeginSteamClient(input, gen);
                },
                onFail: err => ReportSetupFailure(err, false));
            return;
        }

        if (ulong.TryParse(input, out ulong steamLobbyId))
        {
            steam.JoinLobbyById(new CSteamID(steamLobbyId),
                onSuccess: () =>
                {
                    if (gen != setupGeneration)
                    {
                        Debug.Log("[LOBBY] Join cancelled by user - leaving the Steam lobby.");
                        steam.LeaveLobby();
                        return;
                    }
                    BeginSteamClient(input, gen);
                },
                onFail: err => ReportSetupFailure(err, false));
            return;
        }

        ReportSetupFailure("That lobby code is not valid - it has 6 letters/digits (check the host's screen).", true);
        Debug.LogError("[STEAM] Unrecognised lobby address: " + input +
                       " - type a 6-character code, or an IP like 127.0.0.1 to use the local transport " +
                       $"(dev mode currently {(ForceDevTransport ? "ON" : "OFF")}, press F8 to toggle).");
    }

    private const int DevPort = 7777;

    private void UseSceneTransport()
    {
        if (sceneTransport == null) return;

        // Transport.active must be set by hand: Mirror's InitializeSingleton() returns early once the
        // singleton exists, so assigning the 'transport' field alone never reaches Transport.active.
        bool changed = transport != sceneTransport || Transport.active != sceneTransport;

        transport = sceneTransport;
        Transport.active = sceneTransport;

        if (changed)
            InteractionDebug.Log($"[NET] transport restored to {sceneTransport.GetType().Name} for Steam.");
    }

    private KcpTransport EnsureDevTransport()
    {
        KcpTransport kcp = transport as KcpTransport;

        if (kcp == null) kcp = GetComponent<KcpTransport>();
        if (kcp == null) kcp = gameObject.AddComponent<KcpTransport>();

        kcp.Port = DevPort;
        transport = kcp;
        Transport.active = kcp; // see UseSceneTransport: the field alone is not enough

        return kcp;
    }

    private void StartDevHost(string password)
    {
        EnsureDevTransport();

        LobbyPassword = password;
        LobbyID = "DEV";

        InteractionDebug.Log($"[DEV] Steam yok - yerel KCP host baslatiliyor (port {DevPort}). " +
                             "Ikinci instance '127.0.0.1' ile baglanabilir.");

        StartHost();
    }

    private void StartDevClient(string address, string password)
    {
        EnsureDevTransport();

        string host = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();

        networkAddress = host;
        LobbyPassword = password;
        LobbyID = host;

        InteractionDebug.Log($"[DEV] Steam yok - yerel KCP client: {host}:{DevPort}");

        StartClient();
    }

    public void CancelPendingSetup()
    {
        MpTeardownLog.Log("LobbyNetworkManager.CancelPendingSetup");

        setupGeneration++;

        bool aborted = false;

        if (NetworkServer.active)
        {
            SteamLobbyManager.Instance?.LeaveLobby();
            StopHost();
            aborted = true;
        }
        else if (NetworkClient.active && !NetworkClient.isConnected)
        {
            SteamLobbyManager.Instance?.LeaveLobby();
            StopClient();
            aborted = true;
        }

        Debug.Log($"[LOBBY] Back pressed: pending setup cancelled (inFlight={aborted}, gen={setupGeneration}).");

        ResetLobbyState();
    }

    private void BeginSteamClient(string displayId, int gen)
    {
        if (gen != setupGeneration)
        {
            Debug.Log("[LOBBY] Join cancelled by user - not starting the client.");
            return;
        }

        var steam = SteamLobbyManager.Instance;
        if (steam == null || steam.CurrentLobby == CSteamID.Nil)
        {
            Debug.LogError("[STEAM] No Steam lobby - cannot start the client.");
            return;
        }

        string hostSteamId = steam.HostSteamIdString;
        if (string.IsNullOrEmpty(hostSteamId) || hostSteamId == "0")
        {
            Debug.LogError("[STEAM] Could not resolve the host SteamID - cannot start the client.");
            return;
        }

        UseSceneTransport();

        networkAddress = hostSteamId;
        LobbyID = displayId;
        SteamLobbyId = steam.CurrentLobby.m_SteamID;

        Debug.Log($"[STEAM] Starting client. networkAddress={networkAddress} (host SteamID64)");
        StartClient();
    }

    private void HandleInviteCode(string code)
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[STEAM] Invite accepted while already connected - ignored.");
            return;
        }

        GameManager.Instance?.ShowJoinLobby();

        JoinLobbyUI joinUI = FindAnyObjectByType<JoinLobbyUI>(FindObjectsInactive.Include);
        if (joinUI != null) joinUI.PrefillInviteCode(code);
        else Debug.LogWarning($"[STEAM] JoinLobbyUI not found - type the code manually: {code}");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (gameStarted)
            SpawnGamePlayer(conn);
        else
            SpawnLobbyPlayer(conn);
    }

    void SpawnLobbyPlayer(NetworkConnectionToClient conn)
    {
        GameObject lobbyObj = Instantiate(playerPrefab);
        LobbyPlayer lp = lobbyObj.GetComponent<LobbyPlayer>();

        lp.isHost = (conn == NetworkServer.localConnection);

        string requested = conn.authenticationData as string;

        lp.playerName = !string.IsNullOrWhiteSpace(requested)
            ? requested.Trim()
            : (lp.isHost ? "Host" : $"Player {conn.connectionId}");

        NetworkServer.AddPlayerForConnection(conn, lobbyObj);

        StartCoroutine(ForceNameSyncLater(lp));
    }

    private IEnumerator ForceNameSyncLater(LobbyPlayer lp)
    {
        yield return new WaitForSeconds(0.5f);
        if (lp != null && lp.isServer)
        {
            lp.ServerForceNameSync();
            Debug.Log($"[LOBBY] Name sync forced: '{lp.playerName}'");
        }
    }

    void SpawnGamePlayer(NetworkConnectionToClient conn)
    {
        if (gamePlayerPrefab == null)
        {
            Debug.LogError("[MP] gamePlayerPrefab atanmamis - oyun sahnesinde oyuncu olusturulamaz.");
            return;
        }

        Vector3 basePos = Vector3.zero;
        Quaternion baseRot = Quaternion.identity;

        GameObject marker = GameObject.Find(SpawnMarkerName);

        if (MpLevelInstaller.HasAuthoredSpawn)
        {
            basePos = MpLevelInstaller.AuthoredSpawnPosition;
            baseRot = MpLevelInstaller.AuthoredSpawnRotation;

            Debug.Log($"[MP] Spawning at the authored level start {basePos} " +
                      $"(SpawnPoint marker at {(marker != null ? marker.transform.position.ToString() : "none")}).");
        }
        else if (marker != null)
        {
            basePos = marker.transform.position;
            baseRot = marker.transform.rotation;
        }
        else
        {
            Debug.LogWarning($"[MP] '{SpawnMarkerName}' bulunamadi ({UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}) " +
                             "- oyuncu dunya orijinine (0,0,0) konuluyor.");
        }

        int slot = spawnSlot++;
        float angle = (slot % MaxPlayers) * (Mathf.PI * 2f / MaxPlayers);
        Vector3 separation = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;

        GameObject gamePlayer = Instantiate(
            gamePlayerPrefab,
            basePos + separation,
            baseRot
        );

        bool replacedOld = conn.identity != null;
        if (replacedOld)
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer, ReplacePlayerOptions.Destroy);
        else
            NetworkServer.AddPlayerForConnection(conn, gamePlayer);

        CurrencyManager_MP.SendStateTo(conn);

        Debug.Log($"[MP] Game player spawned. conn={conn.connectionId} slot={slot} " +
                  $"pos={basePos + separation} prefab={gamePlayerPrefab.name} replacedOld={replacedOld}");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        string transportName = Transport.active != null ? Transport.active.GetType().Name : "null";
        string portInfo = Transport.active is KcpTransport kcp ? kcp.Port.ToString() : "n/a";
        Debug.Log($"[NET] Server started. transport={transportName} port={portInfo}");

        ChatNetwork.Register();
        VoiceNetwork_MP.Register();
        MpCarSpawner_MP.RegisterPrefabs();
        NetworkPingMonitor_MP.EnsureRegistered();
        CurrencyManager_MP.Register();
        MpLevelNet.Register();
        MatchState_MP.Register();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        connectedOnce = false;
        Debug.Log($"[NET] Client started. target={networkAddress}");

        ChatNetwork.Register();
        VoiceNetwork_MP.Register();
        MpVfxNet_MP.RegisterClient();
        MpCarSpawner_MP.RegisterPrefabs();
        NetworkPingMonitor_MP.EnsureRegistered();
        CurrencyManager_MP.Register();
        MpLevelNet.Register();
        MatchState_MP.Register();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[NET] New connection: connId={conn.connectionId} total={NetworkServer.connections.Count}");

        CurrencyManager_MP.SendStateTo(conn);
        MpLevelNet.SendCurrentTo(conn);
        MatchState_MP.SendStateTo(conn);

        if (gameStarted)
        {
            Debug.LogWarning($"[NET] Game already started - connection rejected: connId={conn.connectionId}");
            conn.Disconnect();
            return;
        }

        // Host + 3. The transport limit already does this; keep it explicit in case maxConnections is edited.
        if (NetworkServer.connections.Count > MaxPlayers)
        {
            Debug.LogWarning($"[NET] Lobby full ({MaxPlayers}) - connection rejected: connId={conn.connectionId}");
            conn.Disconnect();
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.LogWarning($"[NET] Connection closed: connId={conn.connectionId}");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        connectedOnce = true;
        Debug.Log("[NET] CLIENT CONNECTED");

        if (GameManager.Instance != null) GameManager.Instance.ShowLobbyMenu();
    }

    private static readonly System.Reflection.FieldInfo ConnectStateField = typeof(NetworkClient).GetField(
        "connectState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);

    // Mirror keeps connectState internal. "Disconnecting" means we called StopClient/StopHost ourselves.
    private static bool ClientIsDisconnectingLocally()
    {
        object state = ConnectStateField?.GetValue(null);
        return state != null && state.ToString() == "Disconnecting";
    }

    public override void OnClientDisconnect()
    {
        // Read before base: base.OnClientDisconnect -> StopClient flips a remote drop to Disconnecting too.
        bool localDisconnect = ClientIsDisconnectingLocally();

        base.OnClientDisconnect();
        Debug.LogWarning("[NET] Client connection lost.");

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == SelectedGameScenePath)
            Debug.LogError("[MP] The host ended the session while this client was in the "
                           + "match scene. Falling back to MenuScene_MP - check the host's "
                           + "Player.log for '[MP-TEARDOWN]' to see what closed it.");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (!connectedOnce)
        {
            Debug.Log("[LOBBY] Could not connect; staying on the Join screen.");
            ReportSetupFailure(Application.internetReachability == NetworkReachability.NotReachable
                ? MpNotice_MP.Texts.NoInternet
                : "Could not connect to the host. They may have closed the lobby, or one of you is offline.", true);
            return;
        }

        // Disconnecting = we called StopClient/StopHost ourselves (Leave, Main Menu, quit): no warning then.
        bool leftByChoice = localDisconnect || returnToMenuRequested ||
                            quitInProgress || mode == NetworkManagerMode.Host;

        if (!leftByChoice)
            MpNotice_MP.Warning(Application.internetReachability == NetworkReachability.NotReachable
                ? "Disconnected: your internet connection was lost."
                : "Disconnected from the host (connection lost or the host left).");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SwitchToMenuPhase();
            GameManager.Instance.ShowMainMenu();
        }

        ResetLobbyState();

        ReturnToMenuScene();

        Debug.Log("[LOBBY] Lobby closed, returned to the main menu.");
    }

    private void ReturnToMenuScene()
    {
        if (returnToMenuRequested) return;

        if (quitInProgress) return;

        Scene active = SceneManager.GetActiveScene();

        if (active.name == MenuSceneName) return;

        returnToMenuRequested = true;

        Debug.Log($"[LOBBY] Host baglantisi koptu; '{active.name}' -> '{MenuSceneName}' yukleniyor.");

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadMultiplayerMenu();
            return;
        }

        SceneManager.LoadScene(MenuSceneName);
    }

    public bool IsGameStarted => gameStarted;

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (!gameStarted) return;

        Debug.Log($"[SCENE] Server scene ready: {sceneName} - oyuncular hazir oldukca olusturulacak.");

        spawnSlot = 0;

        if (GameManager.Instance != null)
            CurrencyManager_MP.SeedServerTotal(GameManager.Instance.GetStartCurrencyAmount());

        if (GameManager.Instance != null) GameManager.Instance.EnterGamePhase();

        StartCoroutine(LogGamePlayerCountLater());
    }

    private IEnumerator LogGamePlayerCountLater()
    {
        yield return new WaitForSeconds(3f);

        if (!NetworkServer.active) yield break;

        int withPlayer = 0;
        foreach (NetworkConnectionToClient c in NetworkServer.connections.Values)
        {
            if (c != null && c.identity != null) withPlayer++;
        }

        Debug.Log($"[MP] Game scene check: connections={NetworkServer.connections.Count} " +
                  $"withPlayer={withPlayer} (oyuncu sayisi) - {withPlayer} oyuncu bekleniyor: " +
                  $"{(withPlayer == NetworkServer.connections.Count ? "TAMAM" : "EKSIK - loglarda '[MP]' satirlarini kontrol et")}");

        if (withPlayer == 0)
        {
            Debug.LogError("[MP] Oyuncu olusturulamadi. Muhtemel sebepler: " +
                           "(1) istemci Ready/AddPlayer gondermedi, " +
                           "(2) autoCreatePlayer kapali, " +
                           "(3) gameStarted false kaldigi icin SpawnLobbyPlayer cagrildi.");
        }
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        string activePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        if (activePath != SelectedGameScenePath) return;

        Debug.Log($"[SCENE] Client is in the game scene: {activePath}");

        if (GameManager.Instance != null) GameManager.Instance.EnterGamePhase();
    }

    public bool LeaveMatch()
    {
        bool wasHost = NetworkServer.active;
        bool wasClient = NetworkClient.active;

        if (!wasHost && !wasClient) return false;

        Debug.Log($"[LOBBY] Oyun icinden menuye donuluyor: oturum kapatiliyor " +
                  $"(host={wasHost} client={wasClient}).");

        SteamLobbyManager.Instance?.LeaveLobby();

        if (wasHost && wasClient) StopHost();
        else if (wasClient) StopClient();
        else StopServer();

        StartCoroutine(ReturnMenuFallbackRoutine());
        return true;
    }

    private IEnumerator ReturnMenuFallbackRoutine()
    {
        yield return new WaitForSecondsRealtime(1.5f);

        if (returnToMenuRequested || SceneManager.GetActiveScene().name == MenuSceneName) yield break;

        Debug.LogWarning("[LOBBY] Disconnect olayi gelmedi; menuye donus yedek yoldan yapiliyor.");
        ResetLobbyState();
        ReturnToMenuScene();
    }

    public void ResetLobbyState()
    {
        MpTeardownLog.Log("LobbyNetworkManager.ResetLobbyState");

        gameStarted = false;
        SteamLobbyId = 0;
        LobbyID = null;
        LobbyPassword = null;

        SteamLobbyManager.Instance?.LeaveLobby();

        Debug.Log("[LOBBY] Lobby state reset.");
    }

    // Colour per connection, kept after the lobby (LobbyPlayer objects are replaced by game players).
    private static readonly Dictionary<int, int> colorByConnection = new Dictionary<int, int>();

    public static void RememberColor(NetworkConnectionToClient conn, int index)
    {
        if (conn != null) colorByConnection[conn.connectionId] = index;
    }

    public static int ColorFor(NetworkConnectionToClient conn)
    {
        if (conn == null) return -1;
        if (conn.identity != null && conn.identity.TryGetComponent(out LobbyPlayer lobbyPlayer)) return lobbyPlayer.colorIndex;
        return colorByConnection.TryGetValue(conn.connectionId, out int index) ? index : -1;
    }

    public int NextFreeColorIndex(LobbyPlayer exclude = null)
    {
        bool[] used = new bool[MpPlayerColor_MP.Count];

        foreach (LobbyPlayer player in FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
        {
            if (player == null || player == exclude) continue;

            int index = LobbyPlayer.ClampColor(player.colorIndex);

            if (index >= 0 && index < used.Length) used[index] = true;
        }

        for (int i = 0; i < used.Length; i++)
        {
            if (!used[i]) return i;
        }

        return 0;
    }

    public bool AllClientsReady()
    {
        foreach (LobbyPlayer p in FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
        {
            if (p == null || p.isHost) continue;
            if (!p.isReady) return false;
        }

        return true;
    }

    public string ReadySummary()
    {
        int total = 0, ready = 0;

        foreach (LobbyPlayer p in FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
        {
            if (p == null || p.isHost) continue;

            total++;
            if (p.isReady) ready++;
        }

        return total == 0 ? "Waiting for players..." : $"{ready}/{total} Ready";
    }

    public void StartGame()
    {
        if (gameStarted) return;

        if (!AllClientsReady())
        {
            Debug.LogWarning($"[LOBBY] Cannot start - {ReadySummary()}");
            return;
        }

        gameStarted = true;

        string scenePath = SelectedGameScenePath;

        if (MpLevelCatalog.Instance != null)
            MpLevelNet.Instance?.HostSelect(MpLevelCatalog.Instance.SelectedIndex);

        Debug.Log($"[LOBBY] Match starting: {scenePath} " +
                  $"(level {MpLevelCatalog.Instance?.SelectedLabel() ?? "catalog missing"})");

        ServerChangeScene(scenePath);
    }
}
