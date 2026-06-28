using System;
using System.Net;
using Mirror;

/// <summary>
/// The reply a host sends back to a discovering client. Carries the lobby info
/// shown in the server list (name, player count, whether it's locked).
/// </summary>
public struct CustomServerResponse : NetworkMessage
{
    // Filled in by the client after receiving the packet (property => not serialized).
    public IPEndPoint EndPoint { get; set; }

    // Serialized fields (Mirror's weaver generates the read/write automatically):
    public Uri    uri;            // how to connect to the host
    public long   serverId;       // de-dupes a host seen over multiple NICs
    public string lobbyName;
    public ushort currentPlayers;
    public ushort maxPlayers;
    public string passwordHash;   // "" when the lobby has no password

    public bool HasPassword => !string.IsNullOrEmpty(passwordHash);
}
