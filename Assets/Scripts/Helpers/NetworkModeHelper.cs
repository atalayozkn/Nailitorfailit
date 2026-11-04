using Mirror;

// Lets shared gameplay code keep singleplayer-only systems out of network sessions.
public static class NetworkModeHelper
{
    public static bool IsNetworked => NetworkServer.active || NetworkClient.active;
}
