// ============================================================
// File: LobbyUIManager.cs
// Author: Murad
// Created: 30-Jun-2026
// Purpose: Utility methods for managing the lobby UI
// ============================================================

using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    [SerializeField] Transform playerListContent;
    [SerializeField] PlayerListEntryUI playerEntryPrefab;

    [SerializeField] GameObject startGameButton;
    [SerializeField] GameObject readyButton;
    [SerializeField] Button startGameButtonComponent;
    [SerializeField] Button readyButtonComponent;
    [SerializeField] Button leaveLobbyButton;

    void Awake() => Instance = this;

    void Start()
    {
        leaveLobbyButton.onClick.AddListener(OnLeaveLobby);
        startGameButtonComponent.onClick.AddListener(OnStartGame);
    }

    // Adds a new player row to the lobby UI and sets up the buttons for the local player
    public PlayerListEntryUI AddPlayerRow(LobbyPlayer player)
    {
        PlayerListEntryUI row = Instantiate(playerEntryPrefab, playerListContent);
        row.SetName(player.playerName);
        row.UpdateColor(player.isHost, player.isReady);

        if (player.isOwned)
        {
            startGameButton.SetActive(player.isHost);
            readyButton.SetActive(!player.isHost);

            if (!player.isHost)
                readyButtonComponent.onClick.AddListener(
                    () => player.CmdSetReady(!player.isReady));
        }

        return row;
    }

    public void RemovePlayerRow(PlayerListEntryUI row)
    {
        if (row != null) Destroy(row.gameObject);
    }

    // Updates the color of a player row in the lobby UI
    void OnStartGame()
    {
        startGameButtonComponent.interactable = false;
        ((LobbyNetworkManager)NetworkManager.singleton).StartGame();
    }

    void OnLeaveLobby()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else
            NetworkManager.singleton.StopClient();
    }
}