using System;
using System.Collections.Generic;
using Mirror;

/// <summary>
/// Networked per-player state for the in-scene lobby that lives in ExampleScene_MP.
/// Add this to your player prefab (the NetworkManager's "Player Prefab"), which must
/// also have a NetworkIdentity. SyncVars replicate name + ready state to everyone.
/// </summary>
public class LobbyPlayer : NetworkBehaviour
{
    /// <summary>Every spawned player on THIS client. The lobby UI reads this to draw the list.</summary>
    public static readonly List<LobbyPlayer> All = new List<LobbyPlayer>();

    /// <summary>Raised whenever the roster or any ready/name value changes.</summary>
    public static event Action OnPlayersChanged;

    /// <summary>Raised on every client when the host successfully starts the game.</summary>
    public static event Action OnGameStarted;

    [SyncVar(hook = nameof(OnNameChanged))]  public string playerName;
    [SyncVar(hook = nameof(OnReadyChanged))] public bool   isReady;

    public override void OnStartServer()
    {
        // Server assigns a default display name; clients receive it via the SyncVar.
        playerName = $"Player {netId}";
    }

    public override void OnStartClient()
    {
        All.Add(this);
        OnPlayersChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        All.Remove(this);
        OnPlayersChanged?.Invoke();
    }

    void OnNameChanged(string _, string __)  => OnPlayersChanged?.Invoke();
    void OnReadyChanged(bool _, bool __)     => OnPlayersChanged?.Invoke();

    // ---- Client -> Server intents ----

    [Command]
    public void CmdSetReady(bool ready) => isReady = ready;

    /// <summary>Host-only "Start Game". Server re-checks readiness, then tells everyone.</summary>
    [Command]
    public void CmdStartGame()
    {
        if (!AllReady()) return;     // guard against a stale client click
        RpcStartGame();
    }

    [ClientRpc]
    void RpcStartGame() => OnGameStarted?.Invoke();

    /// <summary>True only when there's at least one player and all of them are ready.</summary>
    public static bool AllReady()
    {
        if (All.Count == 0) return false;
        foreach (LobbyPlayer p in All)
            if (p == null || !p.isReady) return false;
        return true;
    }
}
