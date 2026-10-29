using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

public class NetworkPingMonitor_MP : MonoBehaviour
{
    public static NetworkPingMonitor_MP Instance { get; private set; }

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

    private readonly Dictionary<int, PingEntry> serverSidePings =
        new Dictionary<int, PingEntry>();

    private struct PingEntry
    {
        public string name;
        public double rttMs;
    }
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

    public static void EnsureRegistered()
    {
        if (Instance == null) AutoSpawn();
        Instance.RegisterOnce();
    }

    private void RegisterOnce()
    {

        if (NetworkServer.active)
        {
            NetworkServer.UnregisterHandler<PingReportMessage>();
            NetworkServer.RegisterHandler<PingReportMessage>(OnPingReport);
        }

        if (NetworkClient.active)
        {

            NetworkClient.UnregisterHandler<PingTableMessage>();
            NetworkClient.RegisterHandler<PingTableMessage>(OnPingTable, false);
        }

        Debug.Log("[PING] RTT handlers registered. " +
                  $"server={NetworkServer.active} client={NetworkClient.active}");
    }

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

        NetworkServer.SendToAll(new PingTableMessage { table = table }, Channels.Reliable, true);
    }

    private static void OnPingTable(PingTableMessage message)
    {
        LatestTable = message.table;

        tableLogCounter++;
        if (tableLogCounter % 10 == 1)
            Debug.Log("[PING] " + message.table);
    }

    private static int tableLogCounter;
}
