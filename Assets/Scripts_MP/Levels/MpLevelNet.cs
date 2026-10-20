using Mirror;
using UnityEngine;

/// <summary>
/// Replicates the host's level choice (M3, part 2).
///
/// Only the host picks the level, but every client must SEE it in the lobby - so the
/// selection is broadcast with a NetworkMessage (same hand-rolled pattern as the chat,
/// the ping monitor and the networked wallet; no scene or prefab wiring needed).
///
///   host picks   -> MpLevelNet.HostSelect(index) -> server broadcasts the index
///   client joins -> server pushes the current index (SendCurrentTo)
///   everyone     -> MpLevelCatalog.Select(index) + LevelChanged event for the UI
/// </summary>
public class MpLevelNet : MonoBehaviour
{
    public static MpLevelNet Instance { get; private set; }

    /// <summary>Raised on every machine when the level selection changes (UI hook).</summary>
    public static event System.Action<int> LevelChanged;

    public struct LevelMessage : NetworkMessage
    {
        public int index;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpLevelNet>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpLevelNet").AddComponent<MpLevelNet>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Called by LobbyNetworkManager on server/client start (like ChatNetwork.Register).</summary>
    public static void Register()
    {
        // Mirror clears handler tables on shutdown; re-register every session or the
        // second host/join in the same editor answers with "Unknown message id".
        if (NetworkServer.active)
        {
            NetworkServer.UnregisterHandler<LevelMessage>();
            NetworkServer.RegisterHandler<LevelMessage>(OnServerLevel, false);
        }

        if (NetworkClient.active)
        {
            NetworkClient.UnregisterHandler<LevelMessage>();
            NetworkClient.RegisterHandler<LevelMessage>(OnClientLevel, false);
        }
    }

    // ---------------------------------------------------------------- host side

    /// <summary>Host only: change the level and tell everyone (including late joiners via SendCurrentTo).</summary>
    public void HostSelect(int index)
    {
        if (!NetworkServer.active && NetworkClient.active)
        {
            // a client must not change the level (the host owns it)
            InteractionDebug.Log("level selection ignored - only the host picks the level");
            return;
        }

        MpLevelCatalog.Instance?.Select(index);
        InteractionDebug.Log($"level selection -> {MpLevelCatalog.Instance?.SelectedLabel()}");

        if (!NetworkServer.active) return;   // no session: local preview only

        NetworkServer.SendToAll(new LevelMessage { index = MpLevelCatalog.Instance != null ? MpLevelCatalog.Instance.SelectedIndex : 0 },
            Channels.Reliable, sendToReadyOnly: true);
    }

    public void HostNext() => HostSelect((MpLevelCatalog.Instance?.SelectedIndex ?? 0) + 1);

    public void HostPrevious() => HostSelect((MpLevelCatalog.Instance?.SelectedIndex ?? 0) - 1);

    [Server]
    public static void SendCurrentTo(NetworkConnectionToClient conn)
    {
        if (conn == null) return;

        int index = MpLevelCatalog.Instance != null ? MpLevelCatalog.Instance.SelectedIndex : 0;
        conn.Send(new LevelMessage { index = index });
    }

    // ---------------------------------------------------------------- message handlers

    private static void OnServerLevel(NetworkConnectionToClient conn, LevelMessage message)
    {
        // Clients never decide the level; ignore anything they send.
    }

    private static void OnClientLevel(LevelMessage message)
    {
        MpLevelCatalog.Instance?.Select(message.index);
        InteractionDebug.Log($"level selection received -> {MpLevelCatalog.Instance?.SelectedLabel()}");
        LevelChanged?.Invoke(message.index);
    }
}
