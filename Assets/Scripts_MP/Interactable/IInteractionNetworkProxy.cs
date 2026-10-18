namespace Interactions.Networking
{
    using Mirror;
    using UnityEngine;

    /// <summary>
    /// MP interaction proxies implement this to route a station interaction to the
    /// host (server-authoritative).
    ///
    /// requester   - who pressed, so the host can read THEIR carried state from their
    ///               mirrored copy (the host's own player is somebody else).
    /// interactable - the exact IInteractable component that was looked at. A station
    ///               root may host several interactables (mixer panel vs its crank),
    ///               and only this tells them apart.
    /// </summary>
    public interface IInteractionNetworkProxy
    {
        void RequestInteract(NetworkIdentity requester, Component interactable);
    }
}
