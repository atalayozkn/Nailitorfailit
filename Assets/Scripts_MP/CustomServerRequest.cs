using Mirror;

/// <summary>
/// The (empty) message a browsing client broadcasts on the LAN to find lobbies.
/// We don't need any data in the request itself; the response carries everything.
/// </summary>
public struct CustomServerRequest : NetworkMessage { }
