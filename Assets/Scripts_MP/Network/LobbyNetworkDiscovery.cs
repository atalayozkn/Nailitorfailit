// ============================================================
// File:    LobbyNetworkDiscovery.cs
// Author:  Murad
// Created: 30-Jun-2026
// Purpose: Utility script for discovering lobbies in a multiplayer game using Mirror networking
// ============================================================

using System;
using System.Net;
using Mirror;
using Mirror.Discovery;

public class LobbyNetworkDiscovery : NetworkDiscoveryBase<LobbyDiscoveryRequest, LobbyDiscoveryResponse>
{
    public Action<LobbyDiscoveryResponse> onLobbyFound;

    protected override LobbyDiscoveryRequest GetRequest() => new LobbyDiscoveryRequest();

    protected override LobbyDiscoveryResponse ProcessRequest(LobbyDiscoveryRequest request, IPEndPoint endpoint)
    {
        var nm = (LobbyNetworkManager)NetworkManager.singleton;
        return new LobbyDiscoveryResponse
        {
            lobbyId = nm.LobbyID,
            uri = transport.ServerUri()
        };
    }

    protected override void ProcessResponse(LobbyDiscoveryResponse response, IPEndPoint endpoint)
    {
        onLobbyFound?.Invoke(response);
    }
}