using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// The in-scene lobby overlay that lives in ExampleScene_MP. Shows the player list,
/// a Ready toggle for the local player, and a Start Game button for the host.
/// Plain MonoBehaviour: it reads the networked state off LobbyPlayer.
/// </summary>
public class LobbySceneManager : MonoBehaviour
{
    [Header("Lobby Overlay")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_Text   playerListText;

    [Header("Ready")]
    [SerializeField] private Button   readyButton;
    [SerializeField] private TMP_Text readyButtonLabel;

    [Header("Host")]
    [SerializeField] private Button     startButton;          // host only
    [SerializeField] private GameObject notReadyNotification; // "Players aren't ready!"

    private LobbyPlayer Local =>
        NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>() : null;

    private void OnEnable()
    {
        LobbyPlayer.OnPlayersChanged += Refresh;
        LobbyPlayer.OnGameStarted    += HandleGameStarted;
    }

    private void OnDisable()
    {
        LobbyPlayer.OnPlayersChanged -= Refresh;
        LobbyPlayer.OnGameStarted    -= HandleGameStarted;
    }

    private void Start()
    {
        if (readyButton) readyButton.onClick.AddListener(ToggleReady);
        if (startButton) startButton.onClick.AddListener(TryStartGame);
        if (notReadyNotification) notReadyNotification.SetActive(false);
        Refresh();
    }

    private void ToggleReady()
    {
        LobbyPlayer me = Local;
        if (me != null) me.CmdSetReady(!me.isReady);
    }

    private void TryStartGame()
    {
        if (notReadyNotification) notReadyNotification.SetActive(false);

        if (!LobbyPlayer.AllReady())
        {
            if (notReadyNotification) notReadyNotification.SetActive(true);
            return;
        }

        Local?.CmdStartGame();
    }

    private void Refresh()
    {
        // Player list
        if (playerListText)
        {
            StringBuilder sb = new StringBuilder();
            foreach (LobbyPlayer p in LobbyPlayer.All)
            {
                if (p == null) continue;
                sb.AppendLine($"{p.playerName}   {(p.isReady ? "READY" : "...")}");
            }
            playerListText.text = sb.ToString();
        }

        // Ready button label reflects local state
        if (readyButtonLabel && Local != null)
            readyButtonLabel.text = Local.isReady ? "Cancel" : "Ready";

        // Start button only for the host
        if (startButton)
            startButton.gameObject.SetActive(NetworkServer.active);
    }

    private void HandleGameStarted()
    {
        // Game begins: drop the lobby overlay. Hook your own "enable player control"
        // logic to LobbyPlayer.OnGameStarted if you need it.
        if (lobbyPanel) lobbyPanel.SetActive(false);
    }
}
