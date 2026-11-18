
using Mirror;
using UnityEngine;

public class LobbyAuthenticator : NetworkAuthenticator
{
    [HideInInspector] public string serverPassword;
    [HideInInspector] public string clientPassword;

    [HideInInspector] public string clientName;

    struct AuthRequest  : NetworkMessage { public string password; public string playerName; }
    struct AuthResponse : NetworkMessage { public bool ok; public string reason; }

    public override void OnStartServer() =>
        NetworkServer.RegisterHandler<AuthRequest>(OnAuthRequest, false);

    public override void OnServerAuthenticate(NetworkConnectionToClient conn) { }

    // The reason the last connection attempt was refused, so the join screen shows "Lobby is full" instead of the
    // generic "could not connect". Cleared when a new attempt starts.
    public static string LastFailure;

    void OnAuthRequest(NetworkConnectionToClient conn, AuthRequest msg)
    {
        // Steam already blocks non-members, but a lobby whose size was edited (versus = 2, custom max) can still get
        // one connection too many: refuse it here, with a reason the client can read.
        int capacity = NetworkManager.singleton != null ? NetworkManager.singleton.maxConnections : 0;
        if (capacity > 0 && NetworkServer.connections.Count > capacity)
        {
            conn.Send(new AuthResponse { ok = false, reason = $"Lobby is full ({capacity}/{capacity} players)." });
            ServerReject(conn);
            return;
        }

        if (msg.password == serverPassword)
        {
            conn.authenticationData = msg.playerName;

            conn.Send(new AuthResponse { ok = true });
            ServerAccept(conn);
        }
        else
        {
            conn.Send(new AuthResponse { ok = false, reason = "Wrong password" });
            ServerReject(conn);
        }
    }

    public override void OnStartClient() =>
        NetworkClient.RegisterHandler<AuthResponse>(OnAuthResponse, false);

    public override void OnClientAuthenticate() =>
        NetworkClient.Send(new AuthRequest { password = clientPassword, playerName = clientName });

    void OnAuthResponse(AuthResponse msg)
    {
        if (msg.ok) { LastFailure = null; ClientAccept(); return; }

        LastFailure = string.IsNullOrEmpty(msg.reason) ? "Could not join the lobby." : msg.reason;
        Debug.LogWarning($"[LOBBY] Auth failed: {LastFailure}");

        // Shown straight away: the disconnect that follows would otherwise replace it with "Could not connect to the
        // host", which says nothing about the real cause (a full lobby, a wrong password).
        MpNotice_MP.Error(LastFailure);
        ClientReject();
    }
}
