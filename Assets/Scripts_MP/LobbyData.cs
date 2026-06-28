/// <summary>
/// Plain static holder for the lobby settings the host typed in the Host Panel.
/// Lives across scene loads (it's just static memory) so the NetworkManager and
/// the discovery component can read it after StartHost() changes the scene.
/// </summary>
public static class LobbyData
{
    public static string lobbyName    = "Lobby";
    public static int    maxPlayers   = 4;
    public static string password     = "";   // raw text, only ever kept on the host
    public static string passwordHash = "";    // what gets broadcast over discovery

    public static bool HasPassword => !string.IsNullOrEmpty(passwordHash);

    /// <summary>Call this from the Host Panel right before StartHost().</summary>
    public static void Set(string name, int max, string pw)
    {
        lobbyName    = name;
        maxPlayers   = max;
        password     = pw;
        passwordHash = CustomNetworkDiscovery.HashPassword(pw);
    }
}
