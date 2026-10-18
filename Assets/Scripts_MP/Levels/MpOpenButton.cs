using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put this ON the menu button that should open the multiplayer flow, then wire the
/// button's onClick to "MpOpenButton -> Open". Unity's inspector can only call
/// INSTANCE methods, which is why the button cannot call MpBootstrap.Open() directly.
///
/// TWO MODES (pick in the Inspector):
///
///   UseWorkingMpScene  - loads Assets/Scenes_MP/MenuScene_MP.unity, i.e. the
///                        multiplayer setup that already works, unchanged. Nothing is
///                        re-invented; this is the safe default.
///
///   OpenInPlace        - shows the MP UI inside the current menu scene (either the
///                        instance placed there, or the menu prefab assigned on
///                        MpBootstrap). Use it if you want to stay in the SP scene.
/// </summary>
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

            // Mirror's NetworkManager and the Steam managers are DontDestroyOnLoad: if
            // this menu scene has its own MP stack it would SURVIVE the scene load and
            // collide with the target scene's own stack ("Multiple NetworkManagers
            // detected" -> Mirror destroys one -> the host connection dies and a lobby
            // looks like it cannot be created).
            DestroyExistingMpStack();

            GameManager.Instance?.SwitchToMenuPhase();
            SceneManager.LoadScene(mpScenePath);
            return;
        }

        // open in place
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

    /// <summary>Removes the MP stack of the current scene, so the MP scene can bring its own.</summary>
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

        // the Steam wrappers are DontDestroyOnLoad as well
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
