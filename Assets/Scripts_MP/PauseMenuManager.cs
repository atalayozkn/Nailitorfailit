using UnityEngine;
using Mirror;

/// <summary>
/// In-game pause menu for ExampleScene_MP: Continue / Lobby Settings /
/// General Settings / Quit to Main Menu. Quitting tears down the network
/// session, which sends everyone back to the offline scene (MainMenu).
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject lobbySettingsPanel;
    [SerializeField] private GameObject generalSettingsPanel;

    private void Start()
    {
        if (pausePanel)           pausePanel.SetActive(false);
        if (lobbySettingsPanel)   lobbySettingsPanel.SetActive(false);
        if (generalSettingsPanel) generalSettingsPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        if (pausePanel) pausePanel.SetActive(!pausePanel.activeSelf);
    }

    public void Continue()
    {
        if (pausePanel) pausePanel.SetActive(false);
    }

    public void OpenLobbySettings()
    {
        if (lobbySettingsPanel) lobbySettingsPanel.SetActive(true);
    }

    /// <summary>"Back" from the Lobby Settings sub-panel -> back to the pause menu.</summary>
    public void CloseLobbySettings()
    {
        if (lobbySettingsPanel) lobbySettingsPanel.SetActive(false);
    }

    public void OpenGeneralSettings()
    {
        if (generalSettingsPanel) generalSettingsPanel.SetActive(true);
    }

    /// <summary>"Back" from the General Settings sub-panel -> back to the pause menu.</summary>
    public void CloseGeneralSettings()
    {
        if (generalSettingsPanel) generalSettingsPanel.SetActive(false);
    }

    public void QuitToMainMenu()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();   // host
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();  // pure client
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();  // dedicated server

        // StopHost/StopClient/StopServer load offlineScene (MainMenu) for us.
    }
}
