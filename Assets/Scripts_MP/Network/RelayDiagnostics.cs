using System.Reflection;
using Edgegap;
using Mirror;
using UnityEngine;

/// <summary>
/// Diagnostic tool: logs whether the relay connection is alive on both sides,
/// transport-layer parameters and the Edgegap server/client states.
///
/// DISABLED BY DEFAULT - it logs every few seconds and drowns out the messages that
/// actually matter. Tick 'Diagnostics Enabled' in the Inspector when chasing a relay
/// connectivity problem, then untick it again.
/// </summary>
public class RelayDiagnostics : MonoBehaviour
{
    [Tooltip("Off by default: this logs every 3s and floods the Console. " +
             "Enable only while debugging relay connectivity.")]
    [SerializeField] private bool diagnosticsEnabled = false;

    [SerializeField] private float interval = 3f;

    private EdgegapKcpTransport transport;
    private float nextLog;
    private bool loggedServerStateOnce;

    private void Start()
    {
        if (!diagnosticsEnabled)
        {
            enabled = false;
            return;
        }

        transport = NetworkManager.singleton != null
            ? NetworkManager.singleton.transport as EdgegapKcpTransport
            : null;
    }

    private void Update()
    {
        if (!diagnosticsEnabled) return;
        if (Time.time < nextLog) return;
        nextLog = Time.time + interval;

        if (transport == null)
        {
            if (NetworkManager.singleton != null)
                transport = NetworkManager.singleton.transport as EdgegapKcpTransport;
            if (transport == null) return;
        }

        string role = NetworkServer.active && NetworkClient.active ? "HOST"
                    : NetworkServer.active ? "SERVER"
                    : NetworkClient.active ? "CLIENT"
                    : "OFFLINE";

        string serverState = ReadNestedState(transport, "server", "state");
        string clientState = ReadNestedState(transport, "client", "connectionState");

        // TESHIS: server gercekten ping atiyor mu, relay cevap veriyor mu?
        string serverPings = ReadNestedField(transport, "server", "pingSentCount");
        string serverReplies = ReadNestedField(transport, "server", "pingReplyCount");
        string relayActive = ReadNestedField(transport, "server", "RelayActive");

        Debug.Log($"[RelayDiag] role={role} server={NetworkServer.active} client={NetworkClient.active} " +
                  $"clientConnected={NetworkClient.isConnected} " +
                  $"relayAddr={transport.relayAddress}:{transport.relayGameClientPort} " +
                  $"sessionToken={transport.sessionId} userToken={transport.userId} " +
                  $"connections={NetworkServer.connections.Count} " +
                  $"serverState={serverState} clientState={clientState} " +
                  $"serverPingSent={serverPings} serverPingReply={serverReplies} relayActive={relayActive}");

        // Log once when the server side first reaches 'Valid' - proof the relay sees the host.
        if (!loggedServerStateOnce && NetworkServer.active && serverState == "Valid")
        {
            loggedServerStateOnce = true;
            Debug.Log("[RelayDiag] HOST REGISTERED with the relay (serverState=Valid).");
        }
    }

    /// <summary>Reads the transport's protected 'server'/'client' field and one of its members via reflection.</summary>
    private static string ReadNestedField(object root, string fieldName, string innerName)
    {
        try
        {
            FieldInfo fi = null;
            System.Type t = root.GetType();
            while (t != null && fi == null)
            {
                fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                t = t.BaseType;
            }
            if (fi == null) return "n/a";

            object obj = fi.GetValue(root);
            if (obj == null) return "null";

            FieldInfo sf = obj.GetType().GetField(innerName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (sf != null) return sf.GetValue(obj)?.ToString() ?? "null";

            PropertyInfo sp = obj.GetType().GetProperty(innerName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sp != null) return sp.GetValue(obj)?.ToString() ?? "null";

            return "?";
        }
        catch (System.Exception e)
        {
            return "err:" + e.GetType().Name;
        }
    }

    /// <summary>Reads the transport's protected 'server'/'client' field and its state member via reflection.</summary>
    private static string ReadNestedState(object root, string fieldName, string stateName)
    {
        try
        {
            FieldInfo fi = null;
            System.Type t = root.GetType();
            while (t != null && fi == null)
            {
                fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                t = t.BaseType;
            }
            if (fi == null) return "n/a";

            object obj = fi.GetValue(root);
            if (obj == null) return "null";

            FieldInfo sf = obj.GetType().GetField(stateName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (sf != null) return sf.GetValue(obj)?.ToString() ?? "null";

            PropertyInfo sp = obj.GetType().GetProperty(stateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sp != null) return sp.GetValue(obj)?.ToString() ?? "null";

            return "?";
        }
        catch (System.Exception e)
        {
            return "err:" + e.GetType().Name;
        }
    }
}
