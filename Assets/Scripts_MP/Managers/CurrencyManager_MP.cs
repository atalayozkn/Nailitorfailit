using Mirror;
using UnityEngine;

/// <summary>
/// Networked wallet.
///
/// The class existed before but was never placed in any scene or prefab, so
/// 'Instance' was always null: purchases granted items without ever spending, and
/// no wallet change was ever broadcast. It also needed a NetworkIdentity just to
/// carry two RPCs.
///
/// This implementation follows the pattern the chat and the ping monitor already
/// use in this project: a self-spawning DDOL singleton that talks over
/// NetworkMessages, so it needs NO scene or prefab wiring at all.
///
/// Authority: the SERVER holds the single total. Clients send a delta request, the
/// server clamps and answers with the absolute total, which every machine applies -
/// so all clients converge on the same number whatever the packet order.
/// </summary>
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

    // server-side authoritative total (-1 until the first request seeds it)
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

    /// <summary>Called by LobbyNetworkManager on server/client start (like ChatNetwork.Register).</summary>
    public static void Register()
    {
        // Mirror clears the handler tables on shutdown, so registering "once" with a
        // static flag leaves the second session of the same editor without handlers
        // ("Unknown message id: 1487" -> disconnect). Re-register every session.
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

    // ---------------------------------------------------------------- public API

    /// <summary>Request +amount (negative to spend). Routed by CurrencyManager.</summary>
    public void RequestGain(int amount) => RequestChange(Mathf.Abs(amount));

    public void RequestSpend(int amount) => RequestChange(-Mathf.Abs(amount));

    public void RequestChange(int delta)
    {
        if (delta == 0) return;

        Register();

        if (NetworkServer.active)
        {
            // host: apply straight away (the server owns the total)
            ApplyServerChange(delta);
            return;
        }

        if (!NetworkClient.isConnected)
        {
            // no session: plain local change (spend keeps its own UI feedback)
            if (delta >= 0) CurrencyManager.Instance?.ApplyLocalGain(delta);
            else CurrencyManager.Instance?.ApplyLocalSpend(-delta);
            return;
        }

        NetworkClient.Send(new RequestChangeMessage { delta = delta });
    }

    // ---------------------------------------------------------------- server

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

        // the host's own client copy gets the state through the local connection too,
        // but applying it here keeps the host in step even before that arrives.
        ApplyTotal(serverTotal);
    }

    // ---------------------------------------------------------------- client

    private static void OnState(StateMessage message)
    {
        ApplyTotal(message.total);
        InteractionDebug.Log($"wallet: server says {message.total}");
    }

    /// <summary>
    /// SP's CurrencyManager owns the value and updates the CurrencyText it cached at
    /// scene load. In MP that cached text belongs to the level's authored SP player
    /// (the installer disables it), so the Mirror player's HUD is refreshed separately.
    /// </summary>
    private static void ApplyTotal(int total)
    {
        CurrencyManager.Instance?.SetCurrency(total);
        CurrencyManager.Instance?.RefreshLocalCurrencyText();
    }

    /// <summary>
    /// Called when a client finishes connecting: push the current total so the new
    /// machine shows the right number straight away.
    /// </summary>
    public static void SendStateTo(NetworkConnectionToClient conn)
    {
        if (!NetworkServer.active || conn == null) return;

        if (serverTotal < 0)
            serverTotal = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCurrency() : 0;

        conn.Send(new StateMessage { total = serverTotal });
    }

    /// <summary>
    /// Server: starts a match with the same wallet SP would give (GameManager's
    /// startCurrencyAmount) and applies it locally. Players spawned afterwards get the
    /// value through SendStateTo, so every HUD shows the same number from frame one.
    /// </summary>
    public static void SeedServerTotal(int total)
    {
        serverTotal = Mathf.Max(0, total);
        ApplyTotal(serverTotal);
    }
}
