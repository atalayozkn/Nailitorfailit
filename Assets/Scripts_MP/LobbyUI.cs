using UnityEngine;
using Mirror;

/// <summary>
/// Switches between the panels of the multiplayer flow inside the MainMenu scene:
///   Main -> Multiplayer -> (Join) Server list  |  (Create) Host panel
/// Hook the menu buttons to these public methods. Once a host/client connects,
/// Mirror loads ExampleScene_MP and this whole menu is unloaded automatically.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;        // singleplayer / multiplayer / quit
    [SerializeField] private GameObject multiplayerPanel; // join / create
    [SerializeField] private GameObject serverPanel;      // server list (Join flow)
    [SerializeField] private GameObject hostPanel;        // lobby settings (Create flow)

    private void Awake()
    {
        Instance = this;
        ShowMain();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void HideAll()
    {
        if (mainPanel)        mainPanel.SetActive(false);
        if (multiplayerPanel) multiplayerPanel.SetActive(false);
        if (serverPanel)      serverPanel.SetActive(false);
        if (hostPanel)        hostPanel.SetActive(false);
    }

    public void ShowMain()        { HideAll(); if (mainPanel)        mainPanel.SetActive(true); }
    public void ShowMultiplayer() { HideAll(); if (multiplayerPanel) multiplayerPanel.SetActive(true); }
    public void ShowServerList()  { HideAll(); if (serverPanel)      serverPanel.SetActive(true); }
    public void ShowHostPanel()   { HideAll(); if (hostPanel)        hostPanel.SetActive(true); }

    /// <summary>
    /// "Quit to Main Menu" from the menu panels. If a connection somehow exists,
    /// tear it down (which sends us back to the offline scene anyway).
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();

        ShowMain();
    }
}
