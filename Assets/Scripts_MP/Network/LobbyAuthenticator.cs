
using Mirror;
using UnityEngine;

public class LobbyAuthenticator : NetworkAuthenticator
{
    [HideInInspector] public string serverPassword;
    [HideInInspector] public string clientPassword;

    struct AuthRequest  : NetworkMessage { public string password; }
    struct AuthResponse : NetworkMessage { public bool ok; public string reason; }

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
