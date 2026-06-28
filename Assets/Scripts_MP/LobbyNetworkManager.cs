using UnityEngine;
using Mirror;

/// <summary>
/// The project's NetworkManager. Scene transitions are handled by Mirror itself:
///   - offlineScene = MainMenu
///   - onlineScene  = ExampleScene_MP
/// so starting a host OR joining as a client automatically lands everyone in
/// ExampleScene_MP. This subclass just wires LAN advertising + the player cap.
///
/// Place on the "Network Manager" GameObject in MainMenu, alongside the transport
/// and CustomNetworkDiscovery. Keep "Don't Destroy On Load" ticked.
/// </summary>
public class LobbyNetworkManager : NetworkManager
{
    [Header("Lobby")]
    [Tooltip("Discovery component on this same GameObject. Used to advertise the lobby on the LAN while hosting.")]
    [SerializeField] private CustomNetworkDiscovery discovery;

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Honour the player count the host chose in the Host Panel.
        maxConnections = Mathf.Max(2, LobbyData.maxPlayers);

        // Start broadcasting this lobby so browsing clients can find it.
        // (Hosts aren't headless, so the base class won't auto-advertise.)
        if (discovery != null)
        {
            try { discovery.AdvertiseServer(); }
            catch (System.Exception e) { Debug.LogWarning($"Discovery advertise failed: {e.Message}"); }
        }
    }

    public override void OnStopServer()
    {
        if (discovery != null)
            discovery.StopDiscovery();

        base.OnStopServer();
    }
}
