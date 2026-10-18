
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

    /// <summary>Host'un bulundugu Steam lobisinin id'si (LAN modunda 0).</summary>
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

    // Fallback scene if no level catalog is available (kept for safety).
    public const string GameScenePath = "Assets/Scenes_MP/DemoScene_MP.unity";

    /// <summary>
    /// The scene a match will load: the level the HOST picked in the lobby catalog,
    /// falling back to GameScenePath when no catalog is present.
    /// </summary>
    public static string SelectedGameScenePath
    {
        get
        {
            string path = MpLevelCatalog.Instance != null ? MpLevelCatalog.Instance.SelectedScenePath : null;
            return string.IsNullOrEmpty(path) ? GameScenePath : path;
        }
    }

    /// <summary>
    /// Oyun sahnesindeki spawn noktasi. DemoScene_MP'deki (ReSpawnManager altindaki)
    /// nesnenin adi "SpawnPoint"; RespawnManager da ayni Transform'a isaret eder.
    /// </summary>
    public const string SpawnMarkerName = "SpawnPoint";

    /// <summary>
    /// Baglanti koptugunda istemcinin donduruldugu coklu oyuncu menu sahnesi.
    /// SP menusu (Assets/Scenes/MenuScene.unity) artik MP bilesenlerini ICERMEZ; lobi
    /// panelleri, MP_NetworkManager ve Steam bilesenleri Assets/Scenes_MP/MenuScene_MP.unity
    /// icindedir. Bu yuzden MP'den menuye donus de o sahneye yapilir.
    /// </summary>
    public const string MenuSceneName = "MenuScene_MP";

    bool gameStarted = false;

    private int spawnSlot;

    bool connectedOnce = false;

    /// <summary>
    /// Bumps every time the user cancels the create/join flow. Since Steam lobby
    /// creation and joining complete in callbacks that may fire AFTER the user has
    /// already pressed the back button, each pending request records the generation
    /// it started with and simply bails out if the counter has moved on.
    /// </summary>
    int setupGeneration;

    /// <summary>Menuye donus zaten baslatildi mi (sahnenin iki kez yuklenmesini engeller).</summary>
    private bool returnToMenuRequested;

    public override void Awake()
    {
        base.Awake();

        // gamePlayerPrefab, NetworkManager.playerPrefab DEGIL: Mirror uzak istemcilerde
        // spawn mesajini assetId ile cozer (NetworkClient.SpawnPrefab) ve yalnizca
        // playerPrefab + spawnPrefabs listesini kayit eder. Kayitli olmazsa istemciler
        // "Failed to spawn server object, did you forget to add it to the NetworkManager?"
        // hatasi verip oyuncuyu hic olusturmaz. Bu yuzden burada listeye ekliyoruz.
        if (gamePlayerPrefab != null && !spawnPrefabs.Contains(gamePlayerPrefab))
            spawnPrefabs.Add(gamePlayerPrefab);

        // The traffic cars spawn at runtime (CarController_Prototype on the host);
        // Mirror resolves the prefab on the clients through this list.
        if (trafficCarPrefab != null && !spawnPrefabs.Contains(trafficCarPrefab))
            spawnPrefabs.Add(trafficCarPrefab);
    }

    public override void Start()
    {
        base.Start();

        // Davet kabul edildiginde SteamLobbyManager kodu cozer; UI'yi burada yonlendiriyoruz
        // (davet,JoinLobbyUI pasifken de gelebilir, o yuzden paneli acan taraf burada).
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

        // ---------- LAN DEV MODE ----------
        if (steam != null && steam.UseLanDevMode)
        {
            steam.EnsureLanTransport();
            string lanIp = GetLocalIPv4();
            LobbyID = lanIp;
            Debug.Log($"LAN mode: not using Steam, hosting.\n" +
                      $"  For another computer, join with: {lanIp}\n" +
                      $"  For a second instance on this machine: 127.0.0.1");
            StartHost();
            return;
        }
        // ----------------------------------

        if (steam == null)
        {
            Debug.LogError("[STEAM] SteamLobbyManager not found - could not create the lobby.");
            return;
        }

        int gen = ++setupGeneration;
        steam.CreateLobby(
            maxConnections,
            onSuccess: code =>
            {
                // The user may have pressed the back button while Steam was creating the lobby.
                if (gen != setupGeneration)
                {
                    Debug.Log("[LOBBY] Create cancelled by user - discarding the Steam lobby.");
                    steam.LeaveLobby();
                    return;
                }

                LobbyID = code;                       // GameManager.ShowLobbyMenu reads this
                SteamLobbyId = steam.CurrentLobby.m_SteamID;

                Debug.Log($"[STEAM] Lobby ready. code={code} steamLobby={SteamLobbyId}");

                // Unlike the Edgegap relay there is no server-side registration to wait
                // for: the lobby exists and we own it the moment this callback fires.
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
        auth.clientName = DesiredName;          // name picked in the UI before connecting

        string input = LobbyCodeCodec.Canonicalize(userInput);

        var steam = SteamLobbyManager.Instance;

        int gen = ++setupGeneration;

        // ---------- LAN DEV MODE ----------
        if (steam != null && steam.UseLanDevMode)
        {
            string host = (string.IsNullOrEmpty(input) || input == "LAN" || input == "LOCALHOST" || input == "LOCAL")
                ? "127.0.0.1"
                : input;

            steam.EnsureLanTransport();
            Debug.Log($"LAN mode: connecting to host '{host}'. " +
                      $"transport={NetworkManager.singleton.transport?.GetType().Name} " +
                      $"Transport.active={Transport.active?.GetType().Name}");
            NetworkManager.singleton.networkAddress = host;
            LobbyID = host;
            StartClient();
            return;
        }
        // ----------------------------------

        if (steam == null)
        {
            Debug.LogError("[STEAM] SteamLobbyManager not found - could not join.");
            return;
        }

        // 1) 6-character code (main path) - looked up in Steam's lobby list by the
        //    "nif_code" metadata filter.
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

        // 2) A raw SteamID64 / lobby id (what an invite message or the clipboard holds).
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

    /// <summary>
    /// Called by the join/create panels' back buttons: cancels whatever setup is
    /// still in flight so a late Steam callback or pending connect cannot start a
    /// host or yank the player into a lobby after they returned to the menu.
    /// </summary>
    public void CancelPendingSetup()
    {
        setupGeneration++;

        bool aborted = false;

        if (NetworkServer.active)
        {
            // Host was already up - tear it down along with its Steam lobby.
            SteamLobbyManager.Instance?.LeaveLobby();
            StopHost();
            aborted = true;
        }
        else if (NetworkClient.active && !NetworkClient.isConnected)
        {
            // A connect attempt is still running - stop it clean.
            SteamLobbyManager.Instance?.LeaveLobby();
            StopClient();
            aborted = true;
        }

        Debug.Log($"[LOBBY] Back pressed: pending setup cancelled (inFlight={aborted}, gen={setupGeneration}).");

        ResetLobbyState();
    }
    /// <summary>
    /// Runs once the Steam lobby has been entered: points Mirror at the host's SteamID64
    /// (FizzySteamworks does UInt64.Parse on networkAddress) and starts the client.
    /// </summary>
    /// <param name="displayId">The code or SteamID the user typed (shown in the UI).</param>
    /// <param name="gen">The join generation that produced this call; stale calls are discarded.</param>
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

    /// <summary>
    /// Arkadas daveti kabul edildi: sifre Steam'de tutulmadigi icin kullaniciya sorulur.
    /// Join ekranini acip kodu dolduruyoruz; baglanti Join butonuna basinca kurulur.
    /// </summary>
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
            // GameObject.Find pasif nesneleri bulmaz; sahne yuklendikten sonra buraya
            // dusuyorsa "SpawnPoint" pasif ya da adi degismis demektir. Sessizce
            // (0,0,0)'a dusmek yerine gorunur bir uyari birakiyoruz.
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

        // the joining machine should see the current wallet + level immediately
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
        if (!connectedOnce)
        {
            Debug.Log("[LOBBY] Could not connect; staying on the Join screen.");
            return;
        }

        if (GameManager.Instance != null)
        {
            // Musteri oyun sahnesinde kopuyorsa faz "InGame" kalir; o durumda menuye
            // donuste GameManager.SetupGameScene() calisir ve ana menu hic gorunmez
            // (mp* paneller o sahnede yok). Once faza, sonra panele don.
            GameManager.Instance.SwitchToMenuPhase();
            GameManager.Instance.ShowMainMenu();
        }

        ResetLobbyState();

        // Sahne geri donusu: host cikinca istemci oyun sahnesinde UI'siz kaliyordu.
        ReturnToMenuScene();

        Debug.Log("[LOBBY] Lobby closed, returned to the main menu.");
    }

    /// <summary>
    /// Istemciyi ana menu sahnesine dondurur (host oyundan/lobiden ciktiginda).
    ///
    /// Neden gerekli: Mirror'in <c>offlineScene</c> alani bu projede bos ve sahne degisimi
    /// yalnizca host tarafinda <c>ServerChangeScene</c> ile yapiliyor. Host <c>StopHost()</c>
    /// cagirdiginda istemci DemoScene_MP'de (ya da lobide) UI'siz, etkilesimsiz kaliyordu;
    /// Mirror'in kendisi hicbir sahne yuklemiyor. Bu yuzden donusu istemci tarafinda
    /// kendimiz yapiyoruz. Host'un kendisi icin de ayni yol isler (StopHost -> StopClient
    /// -> OnClientDisconnect); lobide duruyorsa asagidaki erken donus sahneyi yeniden yuklemez.
    /// </summary>
    private void ReturnToMenuScene()
    {
        // Ayni anda iki kez cagrilabiliyor (OnClientDisconnect + yedek zamanlayici): sahne
        // IKINCI kez yuklenirse MenuScene'deki NetworkManager/SteamManager/SteamLobbyManager
        // kopyalari DDOL ornekleriyle cakisir ve konsola
        // "Multiple NetworkManagers detected in the scene" + tekil ornek uyarilari duser.
        if (returnToMenuRequested) return;

        Scene active = SceneManager.GetActiveScene();

        // Zaten menude (orn. lobiden ayrilma): sahneyi bosuna yeniden yuklemeyelim.
        if (active.name == MenuSceneName) return;

        returnToMenuRequested = true;

        Debug.Log($"[LOBBY] Host baglantisi koptu; '{active.name}' -> '{MenuSceneName}' yukleniyor.");

        // GameSceneManager kalici (DontDestroyOnLoad) ve yukleme bitince
        // GameManager.OnSceneLoaded() cagirir; boylece menude paneller/kamera yeniden kurulur.
        // LoadMenu() DEGIL: o SP menusunu yukler ve MP'den cikinca lobi panelleri kaybolur.
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

        // NOT: Burada oyunculari ELLE spawn ETMIYORUZ (eski surumde vardi, kaldirildi).
        //
        // Mirror'in sahne yukleme sirasi:
        //   FinishLoadSceneHost() -> SpawnObjects() -> OnServerSceneChanged() -> OnClientSceneChanged()
        // Istemci ancak kendi OnClientSceneChanged icinde Ready() cagirir; dolayisiyla
        // bu noktada AddPlayerForConnection cagirmak:
        //   * sunucuda "NetworkClient can't AddPlayer before being ready" uyarisi
        //   * istemci mesaji hazir olmadan gonderildigi icin istemcide gec gelen spawn
        //     ve ardindan "There is already a player for this connection." LogError'u
        // uretiyordu.
        //
        // Dogru akis: istemci sahneyi yukleyip Ready() + AddPlayer() gonderir, sunucu da
        // OnServerAddPlayer icinden SpawnGamePlayer cagirir (MenuScene'de autoCreatePlayer=1).
        // gameStarted bu asamada zaten true oldugu icin OnServerAddPlayer dogru dala gider.

        if (GameManager.Instance != null) GameManager.Instance.EnterGamePhase();

        // Test teshisi: 3 sn sonra kac baglantinin gercekten oyuncusu var, loga yaz.
        // Boylece "oyuncular spawn olmadi" durumunda sebep tek satirdan gorulur.
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

    /// <summary>
    /// Oyun icindeki "Main Menu" butonlari icin (ReturnMenuHelper): acik Mirror oturumunu
    /// kapatir.
    ///
    /// Donus degeri oturum acikken true olur; bu durumda menuye donusu Mirror'in kopma
    /// yolu (StopHost/StopClient -> OnClientDisconnect -> ReturnToMenuScene) yapar ve
    /// cagiran taraf ayrica sahne yuklememelidir. Oturum yoksa (menude/tek oyunculu)
    /// false doner ve cagiran kendi yolunu izler.
    /// </summary>
    public bool LeaveMatch()
    {
        bool wasHost = NetworkServer.active;
        bool wasClient = NetworkClient.active;

        if (!wasHost && !wasClient) return false;

        Debug.Log($"[LOBBY] Oyun icinden menuye donuluyor: oturum kapatiliyor " +
                  $"(host={wasHost} client={wasClient}).");

        // Steam lobisi varsa dagitilir/yayindan cikilir (LAN modunda no-op).
        SteamLobbyManager.Instance?.LeaveLobby();

        if (wasHost && wasClient) StopHost();
        else if (wasClient) StopClient();
        else StopServer();

        // Emniyet: kopma olayi beklenmedik sekilde gelmezse oyuncu oyun sahnesinde
        // etkilesimsiz kalirdi; kisa bir beklemeden sonra donusu zorluyoruz.
        StartCoroutine(ReturnMenuFallbackRoutine());
        return true;
    }

    private IEnumerator ReturnMenuFallbackRoutine()
    {
        yield return new WaitForSecondsRealtime(1.5f);

        // Donus zaten basladiysa bekleme yeterli olmasa bile ikinci kez yuklemeyelim.
        if (returnToMenuRequested || SceneManager.GetActiveScene().name == MenuSceneName) yield break;

        Debug.LogWarning("[LOBBY] Disconnect olayi gelmedi; menuye donus yedek yoldan yapiliyor.");
        ResetLobbyState();
        ReturnToMenuScene();
    }

    public void ResetLobbyState()
    {
        gameStarted = false;
        SteamLobbyId = 0;
        LobbyID = null;
        LobbyPassword = null;

        // Steam lobisinden cikis. Host ayrilirsa Steam lobiyi dagitir; Edgegap'teki gibi
        // silinecek sunucu tarafi bir kayit yoktur.
        SteamLobbyManager.Instance?.LeaveLobby();

        Debug.Log("[LOBBY] Lobby state reset.");
    }

    /// <summary>Bu makinenin LAN IPv4 adresi (LAN/dev modu icin).</summary>
    public static string GetLocalIPv4()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !System.Net.IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not get the LAN IP: " + e.Message);
        }
        return "127.0.0.1";
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
