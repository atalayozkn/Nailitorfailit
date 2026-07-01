// ============================================================
// File:    LobbyDiscoveryMessages.cs
// Author:  Murad
// Created: 30-Jun-2026
// Purpose: Utility script for defining network messages used in lobby discovery for a multiplayer game using Mirror networking
// ============================================================

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