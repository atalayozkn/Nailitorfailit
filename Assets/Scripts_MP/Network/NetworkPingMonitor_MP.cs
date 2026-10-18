using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

/// <summary>
/// Measures and displays the real connection latency for multiplayer testing.
///
/// Mirror's NetworkTime ping already measures the RTT per client; what it lacks is
/// a shared view: the host cannot see the friends' pings and the friends cannot
/// see each other's. This component closes the loop with two tiny NetworkMessages
/// (no extra NetworkBehaviour, no scene or prefab wiring):
///
///  1. every connected client reports NetworkTime.rtt to the server once a second
///     (PingReportMessage),
///  2. the server merges the reports with the authenticated player names and
///     broadcasts the table to everyone at most once a second
///     (PingTableMessage),
///  3. every machine stores the latest table in NetworkPingMonitor_MP.LatestTable
///     and the debug console writes it into its status header; Debug.Log also
///     mirrors it into player.log for later analysis.
///
/// The component spawns itself before the first scene and survives scene changes
/// (DontDestroyOnLoad), so the ping pipeline starts with the lobby and never stops.
/// </summary>
public class NetworkPingMonitor_MP : MonoBehaviour
{
    public static NetworkPingMonitor_MP Instance { get; private set; }

    /// <summary>The latest "name: xx ms" table the server broadcast; null before the first one.</summary>
    public static string LatestTable { get; private set; }

    public const float ReportIntervalSeconds = 1f;
    private const float TableIntervalSeconds = 1f;

    public struct PingReportMessage : NetworkMessage
    {
        public double rttMs;
    }

    public struct PingTableMessage : NetworkMessage
    {
        public string table;
    }

    // Keyed by connectionId, not by the connection object: this Mirror build's
    // NetworkConnection does not expose isConnected, and NetworkServer.connections
    // is the live source of truth anyway.
    private readonly Dictionary<int, PingEntry> serverSidePings =
        new Dictionary<int, PingEntry>();

    private struct PingEntry
    {
        public string name;
        public double rttMs;
    }
    private bool registered;
    private double nextReportAt;
    private double nextTableAt;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[PING] Latency monitor running. It reports every client's RTT to the host and the table to everyone.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoSpawn()
    {
        if (FindAnyObjectByType<NetworkPingMonitor_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("NetworkPingMonitor_MP").AddComponent<NetworkPingMonitor_MP>();
    }

    void Update()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            // Host: it does not need to report; its RTT to itself is 0.
            if (Time.realtimeSinceStartupAsDouble >= nextTableAt)
            {
                nextTableAt = Time.realtimeSinceStartupAsDouble + TableIntervalSeconds;
                BroadcastTable();
            }
            return;
        }

        if (NetworkClient.isConnected && Time.realtimeSinceStartupAsDouble >= nextReportAt)
        {
            nextReportAt = Time.realtimeSinceStartupAsDouble + ReportIntervalSeconds;
            NetworkClient.Send(new PingReportMessage { rttMs = NetworkTime.rtt * 1000.0 });
        }
    }

    // -------------------------------------------------------------- registration
    /// <summary>Called by LobbyNetworkManager once Mirror's transport stack is up.</summary>
    public static void EnsureRegistered()
    {
        if (Instance == null) AutoSpawn();
        Instance.RegisterOnce();
    }

    private void RegisterOnce()
    {
        if (registered) return;
        registered = true;

        NetworkServer.RegisterHandler<PingReportMessage>(OnPingReport);
        // requireAuthentication: false - the very first table broadcast can arrive
        // while the host's local connection is still handshaking; rejecting it would
        // make Mirror disconnect the host itself.
        NetworkClient.RegisterHandler<PingTableMessage>(OnPingTable, false);
        Debug.Log("[PING] RTT handlers registered.");
    }

    // -------------------------------------------------------------- server side
    private static void OnPingReport(NetworkConnectionToClient conn, PingReportMessage message)
    {
        string name = conn.authenticationData as string;
        if (string.IsNullOrWhiteSpace(name)) name = $"player #{conn.connectionId}";

        Instance.serverSidePings[conn.connectionId] = new PingEntry
        {
            name = name.Trim(),
            rttMs = message.rttMs,
        };
        Instance?.TryBroadcastTable();
    }

    private void TryBroadcastTable()
    {
        if (Time.realtimeSinceStartupAsDouble < nextTableAt) return;
        nextTableAt = Time.realtimeSinceStartupAsDouble + TableIntervalSeconds;
        BroadcastTable();
    }

    private void BroadcastTable()
    {
        // Purge entries whose connection is gone (this Mirror exposes no
        // connection-level 'isConnected' flag), then report what is alive.
        // NetworkServer.connections is the live dictionary of every connection.
        var alive = new HashSet<int>(NetworkServer.connections.Keys);
        foreach (int deadId in new List<int>(serverSidePings.Keys))
            if (!alive.Contains(deadId)) serverSidePings.Remove(deadId);

        var sb = new StringBuilder();
        sb.Append("this host: 0 ms");

        foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
        {
            if (!serverSidePings.TryGetValue(pair.Key, out PingEntry entry)) continue;

            sb.Append(" | ").Append(entry.name).Append(": ").Append(entry.rttMs.ToString("F0")).Append(" ms");
        }

        string table = sb.ToString();
        LatestTable = table;

        // sendToReadyOnly: skip connections that are still handshaking.
        NetworkServer.SendToAll(new PingTableMessage { table = table }, Channels.Reliable, true);
    }

    // -------------------------------------------------------------- client side
    private static void OnPingTable(PingTableMessage message)
    {
        LatestTable = message.table;

        // Also keep it in player.log, but rate limited - a table line every second
        // would bury everything else in a session log.
        tableLogCounter++;
        if (tableLogCounter % 10 == 1)
            Debug.Log("[PING] " + message.table);
    }

    private static int tableLogCounter;
}
