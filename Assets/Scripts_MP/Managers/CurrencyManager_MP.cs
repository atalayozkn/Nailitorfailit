using Mirror;
using UnityEngine;

public class CurrencyManager_MP : MonoBehaviour
{
    public static CurrencyManager_MP Instance { get; private set; }

    public struct RequestChangeMessage : NetworkMessage
    {
        public int delta;
    }

    public struct StateMessage : NetworkMessage
    {
        public int total;
    }

    private static int serverTotal = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<CurrencyManager_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("CurrencyManager_MP").AddComponent<CurrencyManager_MP>();
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
            NetworkServer.UnregisterHandler<RequestChangeMessage>();
            NetworkServer.RegisterHandler<RequestChangeMessage>(OnRequestChange, false);
        }

        if (NetworkClient.active)
        {
            NetworkClient.UnregisterHandler<StateMessage>();
            NetworkClient.RegisterHandler<StateMessage>(OnState, false);
        }
    }

    public void RequestGain(int amount) => RequestChange(Mathf.Abs(amount));

    public void RequestSpend(int amount) => RequestChange(-Mathf.Abs(amount));

    public void RequestChange(int delta)
    {
        if (delta == 0) return;

        Register();

        if (NetworkServer.active)
        {

            ApplyServerChange(delta);
            return;
        }

        if (!NetworkClient.isConnected)
        {

            if (delta >= 0) CurrencyManager.Instance?.ApplyLocalGain(delta);
            else CurrencyManager.Instance?.ApplyLocalSpend(-delta);
            return;
        }

        NetworkClient.Send(new RequestChangeMessage { delta = delta });
    }

    private static void OnRequestChange(NetworkConnectionToClient conn, RequestChangeMessage message)
    {
        ApplyServerChange(message.delta);
    }

    private static void ApplyServerChange(int delta)
    {
        if (serverTotal < 0)
            serverTotal = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCurrency() : 0;

        serverTotal = Mathf.Max(0, serverTotal + delta);

        InteractionDebug.Log($"wallet: {delta:+#;-#;0} -> {serverTotal} (server authoritative)");

        NetworkServer.SendToAll(new StateMessage { total = serverTotal },
            Channels.Reliable, sendToReadyOnly: true);

        ApplyTotal(serverTotal);
    }

    private static void OnState(StateMessage message)
    {
        ApplyTotal(message.total);
        InteractionDebug.Log($"wallet: server says {message.total}");
    }

    private static void ApplyTotal(int total)
    {
        CurrencyManager.Instance?.SetCurrency(total);
        CurrencyManager.Instance?.RefreshLocalCurrencyText();
    }

    public static void SendStateTo(NetworkConnectionToClient conn)
    {
        if (!NetworkServer.active || conn == null) return;

        if (serverTotal < 0)
            serverTotal = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCurrency() : 0;

        conn.Send(new StateMessage { total = serverTotal });
    }

    public static void SeedServerTotal(int total)
    {
        serverTotal = Mathf.Max(0, total);
        ApplyTotal(serverTotal);
    }
}
