
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

    public override void Awake()
    {
        base.Awake();

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

    public void HostLobby(string password)
    {
        LobbyPassword = password;

        ChatNetwork.ClearHistory();

        var auth = (LobbyAuthenticator)authenticator;
        auth.serverPassword = password;
        auth.clientPassword = password;
        auth.clientName = DesiredName;

        var steam = SteamLobbyManager.Instance;

        if (steam == null || !SteamManager.Initialized)
        {
            Debug.LogError("[STEAM] SteamLobbyManager not found - could not create the lobby.");
            return;
        }

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
            onFail: err => Debug.LogError("[STEAM] Could not create the lobby: " + err));
    }

    public void JoinLobby(string userInput, string password)
    {
        LobbyPassword = password;

        ChatNetwork.ClearHistory();

        var auth = (LobbyAuthenticator)authenticator;
        auth.clientPassword = password;
        auth.clientName = DesiredName;

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
                onFail: err => Debug.LogError("[STEAM] Could not join by code: " + err));
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
                onFail: err => Debug.LogError("[STEAM] Could not join by SteamID: " + err));
            return;
        }

        Debug.LogError("[STEAM] Unrecognised lobby address: " + input);
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
        if (marker != null)
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
        string transportName = NetworkManager.singleton.transport != null
            ? NetworkManager.singleton.transport.GetType().Name
            : "null";
        string portInfo = NetworkManager.singleton.transport is KcpTransport kcp ? kcp.Port.ToString() : "n/a";
        Debug.Log($"[NET] Server started. transport={transportName} port={portInfo}");

        ChatNetwork.Register();
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

    public override void OnClientDisconnect()
    {
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
            return;
        }

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
