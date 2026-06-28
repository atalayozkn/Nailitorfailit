using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Mirror;
using Mirror.Discovery;

/// <summary>
/// LAN discovery that advertises our lobby data (name, player count, password hash)
/// instead of Mirror's bare default. The host advertises; browsing clients listen.
/// Put this component on the SAME GameObject as the NetworkManager so it survives
/// the scene change into ExampleScene_MP (DontDestroyOnLoad).
/// </summary>
public class CustomNetworkDiscovery : NetworkDiscoveryBase<CustomServerRequest, CustomServerResponse>
{
    #region Server (host answers browsing clients)

    protected override CustomServerResponse ProcessRequest(CustomServerRequest request, IPEndPoint endpoint)
    {
        try
        {
            return new CustomServerResponse
            {
                serverId       = ServerId,
                uri            = transport.ServerUri(),
                lobbyName      = LobbyData.lobbyName,
                currentPlayers = (ushort)NetworkServer.connections.Count,
                maxPlayers     = (ushort)LobbyData.maxPlayers,
                passwordHash   = LobbyData.passwordHash
            };
        }
        catch (NotImplementedException)
        {
            Debug.LogError($"Transport {transport} does not support network discovery");
            throw;
        }
    }

    #endregion

    #region Client (browsing for lobbies)

    protected override CustomServerRequest GetRequest() => new CustomServerRequest();

    protected override void ProcessResponse(CustomServerResponse response, IPEndPoint endpoint)
    {
        // Remember who sent it...
        response.EndPoint = endpoint;

        // ...and use the real source IP as the host (the advertised Uri host may be unroutable).
        UriBuilder realUri = new UriBuilder(response.uri)
        {
            Host = response.EndPoint.Address.ToString()
        };
        response.uri = realUri.Uri;

        OnServerFound.Invoke(response);
    }

    #endregion

    /// <summary>
    /// SHA-256 hex of a password. Empty/null in -> "" out (so "no password" hashes
    /// equal on both sides). Shared by the host (to set the hash) and the joining
    /// client (to compare what the user typed).
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return "";

        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));

        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
