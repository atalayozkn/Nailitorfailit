
using System;
using Mirror;

public struct LobbyDiscoveryRequest : NetworkMessage
{
}

public struct LobbyDiscoveryResponse : NetworkMessage
{
    public string lobbyId;
    public Uri uri;
}
