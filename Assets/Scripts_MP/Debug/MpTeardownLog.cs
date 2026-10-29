using Mirror;
using UnityEngine;

public static class MpTeardownLog
{
    public static void Log(string caller)
    {
        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;

        bool server = NetworkServer.active;
        bool client = NetworkClient.active;
        bool matchFlag = nm != null && nm.IsGameStarted;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        Debug.LogWarning(
            $"[MP-TEARDOWN] {caller}: server={server} client={client} " +
            $"isGameStarted={matchFlag} scene='{scene}'");
    }
}
