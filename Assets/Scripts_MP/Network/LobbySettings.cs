// ============================================================
// File:    LobbySettings.cs
// Author:  Murad
// Created: 30-Jun-2026
// Purpose: Utility methods for syncing the lobby ID across the network
// ============================================================

using Mirror;

public class LobbySettings : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnSessionIdChanged))]
    public string sessionId;

    public static LobbySettings Instance;

    void Awake() => Instance = this;

    [Server]
    public void ServerSetSessionId(string id) => sessionId = id;

    void OnSessionIdChanged(string oldVal, string newVal)
    {
        if (LobbyIdToggle.Instance != null)
            LobbyIdToggle.Instance.SetLobbyId(newVal);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}