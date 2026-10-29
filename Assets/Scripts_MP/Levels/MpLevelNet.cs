using Mirror;
using UnityEngine;

public class MpLevelNet : MonoBehaviour
{
    public static MpLevelNet Instance { get; private set; }

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

    public static void Register()
    {

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

    public void HostSelect(int index)
    {
        if (!NetworkServer.active && NetworkClient.active)
        {

            InteractionDebug.Log("level selection ignored - only the host picks the level");
            return;
        }

        MpLevelCatalog.Instance?.Select(index);
        InteractionDebug.Log($"level selection -> {MpLevelCatalog.Instance?.SelectedLabel()}");

        if (!NetworkServer.active) return;

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

    private static void OnServerLevel(NetworkConnectionToClient conn, LevelMessage message)
    {

    }

    private static void OnClientLevel(LevelMessage message)
    {
        MpLevelCatalog.Instance?.Select(message.index);
        InteractionDebug.Log($"level selection received -> {MpLevelCatalog.Instance?.SelectedLabel()}");
        LevelChanged?.Invoke(message.index);
    }
}
