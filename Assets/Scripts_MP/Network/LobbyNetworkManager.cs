// ============================================================
// File:    LobbyNetworkManager.cs
// Author:  Murad
// Created: 29-Jun-2026
// Purpose: Utility class for managing lobby networking in a multiplayer game using Mirror
// ============================================================

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

    bool gameStarted = false;

    public void HostLobby(string password)
    {
        LobbyPassword = password;

        var auth = (LobbyAuthenticator)authenticator;
        auth.serverPassword = password;
        auth.clientPassword = password;

        EdgegapRelayManager.Instance.CreateRelaySession(
            maxConnections,
            onSuccess: (sessionId) =>
            {
                edgegapSessionId = sessionId;
                LobbyID = sessionId;
                StartHost();
            },
            onFail: () =>
            {
                Debug.LogError("Relay oluşturulamadı!");
            }
        );
    }

    public void JoinLobby(string sessionId, string password)
    {
        var auth = (LobbyAuthenticator)authenticator;
        auth.clientPassword = password;

        EdgegapRelayManager.Instance.JoinRelaySession(
            sessionId,
            onSuccess: () =>
            {
                StartClient();
            },
            onFail: () =>
            {
                Debug.LogError("Relay'e katılınamadı!");
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
        lp.playerName = lp.isHost ? "Host" : $"Player {conn.connectionId}";

        NetworkServer.AddPlayerForConnection(conn, lobbyObj);
    }

    void SpawnGamePlayer(NetworkConnectionToClient conn)
    {
        Transform spawnPoint = GetStartPosition();
        GameObject gamePlayer = Instantiate(
            gamePlayerPrefab,
            spawnPoint != null ? spawnPoint.position : Vector3.zero,
            spawnPoint != null ? spawnPoint.rotation : Quaternion.identity
        );

        if (conn.identity == null)
            NetworkServer.AddPlayerForConnection(conn, gamePlayer);
        else
            NetworkServer.ReplacePlayerForConnection(conn, gamePlayer, ReplacePlayerOptions.Destroy);
    }


    public void StartGame()
    {
        if (gameStarted) return;
        gameStarted = true;

        ServerChangeScene("GameScene");
    } 

    // public void StartGame()
    // {
    //     if (gameStarted) return;
    //     gameStarted = true;

    //     Debug.Log($"StartGame çağrıldı. Bağlı oyuncu sayısı: {NetworkServer.connections.Count}");

    //     foreach (var conn in NetworkServer.connections.Values)
    //     {
    //         Debug.Log($"conn {conn.connectionId} — identity null mu: {conn.identity == null}");

    //         if (conn.identity == null) continue;

    //         Debug.Log($"gamePlayerPrefab null mu: {gamePlayerPrefab == null}");

    //         Transform spawnPoint = GetStartPosition();
    //         GameObject gamePlayer = Instantiate(
    //             gamePlayerPrefab,
    //             spawnPoint != null ? spawnPoint.position : Vector3.zero,
    //             spawnPoint != null ? spawnPoint.rotation : Quaternion.identity
    //         );

    //         Debug.Log($"gamePlayer oluşturuldu: {gamePlayer.name}, conn {conn.connectionId} için replace ediliyor");
    //         NetworkServer.ReplacePlayerForConnection(conn, gamePlayer, ReplacePlayerOptions.Destroy);
    //     }

    //     ServerChangeScene("GameScene");
    // }
}