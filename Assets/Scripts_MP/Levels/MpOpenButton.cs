using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MpOpenButton : MonoBehaviour
{
    public enum OpenMode
    {
        UseWorkingMpScene,
        OpenInPlace,
    }

    [SerializeField] private OpenMode mode = OpenMode.UseWorkingMpScene;

    [Tooltip("Scene used by 'UseWorkingMpScene'.")]
    [SerializeField] private string mpScenePath = "Assets/Scenes_MP/MenuScene_MP.unity";

    [Header("OpenInPlace")]
    [Tooltip("Objects hidden while the MP menu is open (e.g. the SP menu panel).")]
    [SerializeField] private GameObject[] hideWhileOpen;

    [Tooltip("Objects shown while the MP menu is open (e.g. an MP canvas placed in the scene).")]
    [SerializeField] private GameObject[] showWhileOpen;

    [SerializeField] private bool logClicks = true;

    public void Open()
    {
        if (logClicks)
            Debug.Log($"[MP] Multiplayer button pressed (mode={mode})");

        if (mode == OpenMode.UseWorkingMpScene)
        {
            if (string.IsNullOrEmpty(mpScenePath))
            {
                Debug.LogError("[MP] No MP scene path set on MpOpenButton.");
                return;
            }

            DestroyExistingMpStack();

            GameManager.Instance?.SwitchToMenuPhase();
            SceneManager.LoadScene(mpScenePath);
            return;
        }

        SetActive(hideWhileOpen, false);
        SetActive(showWhileOpen, true);

        MpBootstrap.Open();
    }

    public void Close()
    {
        if (mode == OpenMode.OpenInPlace)
        {
            SetActive(showWhileOpen, false);
            SetActive(hideWhileOpen, true);
        }

        MpBootstrap.Close();
    }

    private static void DestroyExistingMpStack()
    {
        LobbyNetworkManager manager = NetworkManager.singleton as LobbyNetworkManager;

        if (manager != null)
        {
            if (NetworkServer.active) manager.StopHost();
            else if (NetworkClient.active) manager.StopClient();

            Debug.Log("[MP] destroying this scene's MP stack before loading the MP scene " +
                      "(prevents duplicate NetworkManagers / Steam managers).");
            Destroy(manager.gameObject);
        }

        foreach (SteamManager steam in FindObjectsByType<SteamManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(steam.gameObject);

        foreach (SteamLobbyManager lobby in FindObjectsByType<SteamLobbyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(lobby.gameObject);
    }

    private static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (GameObject go in objects)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
