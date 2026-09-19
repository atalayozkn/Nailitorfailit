
using System.Collections;
using System.Collections.Generic;
using kcp2k;
using Mirror;
using UnityEngine;

public class LobbyNetworkManager : NetworkManager
{
    [Header("Lobby")]
    public GameObject gamePlayerPrefab;
    public GameObject lobbySettingsPrefab;

    public string edgegapSessionId { get; private set; }
    public string LobbyID { get; private set; }
    public string LobbyPassword { get; private set; }

    public static string DesiredName { get; private set; } = "";

    public static void SetDesiredName(string name)
    {
        DesiredName = name == null ? "" : name.Trim();
    }

    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;

    public const string GameScenePath = "Assets/Scenes_MP/Level_01_Arda_MP.unity";

    public const string SpawnMarkerName = "MP_SpawnPoint";

    bool gameStarted = false;

    private int spawnSlot;

    bool connectedOnce = false;

    public void HostLobby(string password)
    {
        LobbyPassword = password;

        ChatNetwork.ClearHistory();

        var auth = (LobbyAuthenticator)authenticator;
        auth.serverPassword = password;
        auth.clientPassword = password;
        auth.clientName = DesiredName;

        // ---------- LAN DEV MODE ----------
        if (EdgegapRelayManager.Instance != null && EdgegapRelayManager.Instance.UseLanDevMode)
        {
            EdgegapRelayManager.Instance.EnsureLanTransport();
            string lanIp = GetLocalIPv4();
            LobbyID = lanIp;
            Debug.Log($"LAN mode: not using relay, hosting.\n" +
                      $"  For another computer, join with: {lanIp}\n" +
                      $"  For a second instance on this machine: 127.0.0.1");
            StartHost();
            return;
        }
        // ----------------------------------

        if (EdgegapRelayManager.Instance == null)
        {
            Debug.LogError("[RELAY] EdgegapRelayManager not found (it may have been destroyed) - could not create the lobby.");
            return;
        }

        EdgegapRelayManager.Instance.CreateRelaySession(
            maxConnections,
            onSuccess: (sessionId) =>
            {
                edgegapSessionId = sessionId;

                LobbyID = LobbyCodeCodec.Code6(sessionId);
                Debug.Log($"Lobby code (6 chars): {LobbyID}  [relay session: {sessionId}]");

                StartHost();
                StartCoroutine(WatchRelayHost(sessionId));
            },
            onFail: () =>
            {
                Debug.LogError("Could not create the relay session!");
            }
        );
    }

    public void JoinLobby(string userInput, string password)
    {
        LobbyPassword = password;

        ChatNetwork.ClearHistory();

        var auth = (LobbyAuthenticator)authenticator;
        auth.clientPassword = password;
        auth.clientName = DesiredName;          // name picked in the UI before connecting

        string input = userInput.Trim().ToUpperInvariant();

        // ---------- LAN DEV MODE ----------
        if (EdgegapRelayManager.Instance != null && EdgegapRelayManager.Instance.UseLanDevMode)
        {
            string host = (string.IsNullOrEmpty(input) || input == "LAN" || input == "LOCALHOST" || input == "LOCAL")
                ? "127.0.0.1"
                : input;

            EdgegapRelayManager.Instance.EnsureLanTransport();
            Debug.Log($"LAN mode: connecting to host '{host}'. " +
                      $"transport={NetworkManager.singleton.transport?.GetType().Name} " +
                      $"Transport.active={Transport.active?.GetType().Name}");
            NetworkManager.singleton.networkAddress = host;
            LobbyID = host;
            StartClient();
            return;
        }
        // ----------------------------------

        // 1) 6-character short code (main path) - matched against the active session list.
        if (LobbyCodeCodec.IsValidShortCode(input))
        {
            if (EdgegapRelayManager.Instance == null)
            {
                Debug.LogError("[RELAY] EdgegapRelayManager not found - could not join by code.");
                return;
            }

            EdgegapRelayManager.Instance.ResolveShortCode(input,
                onSuccess: relaySessionId => JoinRelayInternal(input, relaySessionId),
                onFail: () =>
                {
                    Debug.LogError("Could not resolve the 6-character code - the relay session may not be active.");
                });
            return;
        }

        // 2) 10-character full code (offline codec).
        if (LobbyCodeCodec.TryDecode(input, out string decoded))
        {
            JoinRelayInternal(input, decoded);
            return;
        }

        // 3) Last resort: the value may be Edgegap's own session id.
        JoinRelayInternal(input, input);
    }

    private void JoinRelayInternal(string displayId, string sessionId)
    {
        if (EdgegapRelayManager.Instance == null)
        {
            Debug.LogError("[RELAY] EdgegapRelayManager not found - could not join the relay.");
            return;
        }

        EdgegapRelayManager.Instance.JoinRelaySession(
            sessionId,
            onSuccess: () =>
            {
                edgegapSessionId = sessionId;
                LobbyID = displayId;

                StartClient();
                StartCoroutine(WatchRelayClient());
            },
            onFail: () =>
            {
                Debug.LogError("Could not join the relay!");
            }
        );
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
        Vector3 basePos = Vector3.zero;
        Quaternion baseRot = Quaternion.identity;

        GameObject marker = GameObject.Find(SpawnMarkerName);
        if (marker != null)
        {
            basePos = marker.transform.position;
            baseRot = marker.transform.rotation;
        }

        int slot = spawnSlot++;
        float angle = (slot % MaxPlayers) * (Mathf.PI * 2f / MaxPlayers);
        Vector3 separation = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;

        GameObject gamePlayer = Instantiate(
            gamePlayerPrefab,
            basePos + separation,
            baseRot
        );

        if (conn.identity == null)
            NetworkServer.AddPlayerForConnection(conn, gamePlayer);
        else
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer, ReplacePlayerOptions.Destroy);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        string portInfo = NetworkManager.singleton.transport is KcpTransport kcp ? kcp.Port.ToString() : "?";
        Debug.Log($"[NET] Server started. port={portInfo}");

        ChatNetwork.Register();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        connectedOnce = false;
        Debug.Log($"[NET] Client started. target={networkAddress}");

        ChatNetwork.Register();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[NET] New connection: connId={conn.connectionId} total={NetworkServer.connections.Count}");

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

        if (GameManager.Instance != null) GameManager.Instance.ShowMainMenu();

        ResetLobbyState();

        Debug.Log("[LOBBY] Lobby closed, returned to the main menu.");
    }

    public bool IsGameStarted => gameStarted;

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (!gameStarted) return;

        Debug.Log($"[SCENE] Server scene ready: {sceneName} - converting players.");

        spawnSlot = 0;

        List<NetworkConnectionToClient> conns = new List<NetworkConnectionToClient>(NetworkServer.connections.Values);

        foreach (NetworkConnectionToClient conn in conns)
        {
            if (conn != null) SpawnGamePlayer(conn);
        }

        if (GameManager.Instance != null) GameManager.Instance.EnterGamePhase();
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        string activePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        if (activePath != GameScenePath) return;

        Debug.Log($"[SCENE] Client is in the game scene: {activePath}");

        if (GameManager.Instance != null) GameManager.Instance.EnterGamePhase();
    }

    private int relayHostRetries;
    private string failedTowerIp;
    private const int MaxRelayHostRetries = 3;
    private const float RelayValidTimeout = 30f;

    private IEnumerator WatchRelayHost(string sessionId)
    {
        float waited = 0f;
        while (waited < RelayValidTimeout)
        {
            yield return new WaitForSeconds(1f);
            waited += 1f;

            if (EdgegapRelayManager.Instance != null && EdgegapRelayManager.Instance.IsRelayServerValid())
            {
                Debug.Log("[RELAY] Host registered with the relay (Valid) - players can connect.");
                relayHostRetries = 0;
                failedTowerIp = null;
                yield break;
            }
        }

        string tower = EdgegapRelayManager.Instance != null ? EdgegapRelayManager.Instance.LastRelayIp : "?";

        if (relayHostRetries >= MaxRelayHostRetries)
        {
            Debug.LogError($"[RELAY] The relay tower ({tower}) did not respond after {MaxRelayHostRetries} attempts. " +
                           "This may be a temporary issue on Edgegap's side: wait 1-2 minutes, press Leave, " +
                           "then try Create Lobby again.");
            yield break;
        }

        relayHostRetries++;

        if (!string.IsNullOrEmpty(failedTowerIp) && failedTowerIp == tower)
            Debug.LogWarning($"[RELAY] The same tower ({tower}) was assigned again and still does not respond - " +
                             "this tower may be faulty on Edgegap's side. Retrying...");

        failedTowerIp = tower;
        Debug.LogWarning($"[RELAY] tower={tower} did not respond within {RelayValidTimeout}s - " +
                         $"retrying with a new session ({relayHostRetries}/{MaxRelayHostRetries})...");

        if (EdgegapRelayManager.Instance != null) EdgegapRelayManager.Instance.DeleteRelaySession(edgegapSessionId);

        NetworkManager.singleton.StopHost();

        yield return new WaitForSeconds(2f);

        if (EdgegapRelayManager.Instance == null)
        {
            Debug.LogError("[RELAY] Retry cancelled: EdgegapRelayManager no longer exists.");
            yield break;
        }

        HostLobby(LobbyPassword);
    }

    private IEnumerator WatchRelayClient()
    {
        float waited = 0f;
        while (waited < 12f)
        {
            yield return new WaitForSeconds(1f);
            waited += 1f;

            if (EdgegapRelayManager.Instance != null && EdgegapRelayManager.Instance.IsRelayClientValid())
            {
                Debug.Log("[RELAY] Client relay'e kaydoldu (Valid).");
                yield break;
            }
        }

        Debug.LogError("[RELAY] Client relay'e kaydolamadi. Kod aktif bir oturuma ait olmayabilir " +
                       "veya ag relay trafigini engelliyor.");
    }

    public void ResetLobbyState()
    {
        gameStarted = false;
        relayHostRetries = 0;
        edgegapSessionId = null;
        LobbyID = null;
        LobbyPassword = null;
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

        Debug.Log($"[LOBBY] Match starting: {GameScenePath}");
        ServerChangeScene(GameScenePath);
    }

}
