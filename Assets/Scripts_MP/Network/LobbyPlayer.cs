// ============================================================
// File: LobbyPlayer.cs
// Author: Murad
// Created: 30-Jun-2026
// Purpose: Utility methods for managing player information in the lobby
// ============================================================

using Mirror;

public class LobbyPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName;

    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady;

    [SyncVar(hook = nameof(OnHostChanged))]
    public bool isHost;

    PlayerListEntryUI uiRow;

    public override void OnStartClient()
    {
        if (LobbyUIManager.Instance != null)
            uiRow = LobbyUIManager.Instance.AddPlayerRow(this);
    }

    public override void OnStopClient()
    {
        if (uiRow != null && LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.RemovePlayerRow(uiRow);
    }

    void OnNameChanged(string oldVal, string newVal) => uiRow?.SetName(newVal);
    void OnReadyChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(isHost, newVal);
    void OnHostChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(newVal, isReady);

    // Command to set the player's ready status (sycnhronized across the network)
    [Command]
    public void CmdSetReady(bool ready)
    {
        if (isHost) return;
        isReady = ready;
    }
}