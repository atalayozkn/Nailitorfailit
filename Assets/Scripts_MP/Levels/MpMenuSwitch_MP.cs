using Mirror;
using UnityEngine;

// The multiplayer menu lives in the main menu scene now (panel "MainMenu_MP" next to the singleplayer "MainMenu"):
// the Multiplayer button swaps the two panels instead of loading another scene. While a lobby/match connection is
// alive (host/client started, returning from a match, -host/-join test arguments) the multiplayer panel is shown
// by itself, so the lobby is never hidden behind the singleplayer buttons.
public class MpMenuSwitch_MP : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject mpPanel;

    private static MpMenuSwitch_MP instance;

    public bool IsMpOpen => mpPanel != null && mpPanel.activeSelf;

    private void Awake() => instance = this;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start() => AutoOpenWhenConnected();

    private void Update() => AutoOpenWhenConnected();

    private void AutoOpenWhenConnected()
    {
        if (mpPanel == null || mpPanel.activeSelf) return;
        if (NetworkServer.active || NetworkClient.active) Open();
    }

    // Multiplayer button.
    public void Open()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (mpPanel != null) mpPanel.SetActive(true);

        GameManager.Instance?.SwitchToMenuPhase();
        Debug.Log("[MP-MENU] multiplayer panel opened (same scene).");
    }

    // Back button of the multiplayer panel: leave the lobby/connection, show the singleplayer menu.
    public void Close()
    {
        MpMenuExit.ExitToMainMenu();

        if (mpPanel != null) mpPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        Debug.Log("[MP-MENU] back to the main panel.");
    }

    public static void CloseIfPresent()
    {
        if (instance != null) instance.Close();
    }
}
