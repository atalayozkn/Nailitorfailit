using Mirror;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MpMenuExit
{
    public static void ExitToMainMenu()
    {
        MpTeardownLog.Log("MpMenuExit.ExitToMainMenu");
        Debug.Log("[MP-UI] exit to main menu pressed");

        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        if (nm != null && (NetworkServer.active || NetworkClient.active))
        {
            SteamLobbyManager.Instance?.LeaveLobby();

            if (NetworkServer.active && NetworkClient.active) nm.StopHost();
            else if (NetworkClient.active) nm.StopClient();
            else nm.StopServer();

            nm.ResetLobbyState();
            Debug.Log("[MP-UI] exit: network stopped.");
        }

        MpInPlaceToggle inPlace = Object.FindAnyObjectByType<MpInPlaceToggle>(FindObjectsInactive.Include);
        if (inPlace != null)
        {
            inPlace.Close();
            Debug.Log("[MP-UI] exit: closed the in-place MP menu.");
            return;
        }

        if (MpMenuUI.Instance != null) MpMenuUI.Instance.HideAll();

        GameSceneManager sceneManager = GameSceneManager.Instance;
        if (sceneManager == null)
        {
            Debug.LogWarning("[MP-UI] exit: GameSceneManager is missing, cannot load the main menu.");
            return;
        }

        if (sceneManager.GetActiveSceneIndex() != 0)
        {
            Debug.Log("[MP-UI] exit: loading the singleplayer menu scene.");
            sceneManager.LoadMenu();
        }
    }
}

public static class MpMenuExitBinder
{
    private static readonly System.Collections.Generic.HashSet<int> Bound =
        new System.Collections.Generic.HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Bind();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Bind();

    private static void Bind()
    {
        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        if (nm != null && nm.IsGameStarted) return;

        if (!SceneManager.GetActiveScene().name.Contains("Menu")) return;

        int bound = 0;

        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.name.IndexOf("backtomenu", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!HasDeadCall(button.onClick)) continue;
            if (!Bound.Add(button.GetInstanceID())) continue;

            button.onClick.AddListener(MpMenuExit.ExitToMainMenu);
            bound++;
            Debug.Log($"[MP-UI] bound exit button '{button.name}' (its onClick target was missing).", button);
        }

        if (bound > 0) Debug.Log($"[MP-UI] exit binder: {bound} button(s) wired.");
    }

    private static bool HasDeadCall(UnityEvent unityEvent)
    {
        int persistent = unityEvent.GetPersistentEventCount();
        if (persistent == 0) return true;

        for (int i = 0; i < persistent; i++)
        {
            if (unityEvent.GetPersistentTarget(i) == null) return true;
        }

        return false;
    }
}
