// ============================================================
// File: LobbyAuthenticator.cs
// Author: Murad
// Created: 30-Jun-2026
// Purpose: Utility methods for authenticating lobby connections
// ============================================================

using Mirror;
using UnityEngine;

public class LobbyAuthenticator : NetworkAuthenticator
{
    [HideInInspector] public string serverPassword; // set by host
    [HideInInspector] public string clientPassword; // set before joining

    struct AuthRequest  : NetworkMessage { public string password; }
    struct AuthResponse : NetworkMessage { public bool ok; public string reason; }

    // ---------- SERVER ----------
    public override void OnStartServer() =>
        NetworkServer.RegisterHandler<AuthRequest>(OnAuthRequest, false);

    public override void OnServerAuthenticate(NetworkConnectionToClient conn) { }

    void OnAuthRequest(NetworkConnectionToClient conn, AuthRequest msg)
    {
        if (msg.password == serverPassword)
        {
            conn.Send(new AuthResponse { ok = true });
            ServerAccept(conn);
        }
        else
        {
            conn.Send(new AuthResponse { ok = false, reason = "Wrong password" });
            ServerReject(conn);
        }
    }

    // ---------- CLIENT ----------
    public override void OnStartClient() =>
        NetworkClient.RegisterHandler<AuthResponse>(OnAuthResponse, false);

    public override void OnClientAuthenticate() =>
        NetworkClient.Send(new AuthRequest { password = clientPassword });

    void OnAuthResponse(AuthResponse msg)
    {
        if (msg.ok) ClientAccept();
        else { Debug.LogWarning($"Auth failed: {msg.reason}"); ClientReject(); }
    }
}