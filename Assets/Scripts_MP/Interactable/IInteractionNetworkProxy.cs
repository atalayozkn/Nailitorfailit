namespace Interactions.Networking
{
    using Mirror;
    using UnityEngine;

    public interface IInteractionNetworkProxy
    {
        void RequestInteract(NetworkIdentity requester, Component interactable);
    }
}
