using Mirror;
using UnityEngine;

public static class PlayerInteractionResolver
{

    public static PlayerInteractionHandler Local()
    {
        if (NetworkClient.active && NetworkClient.localPlayer != null &&
            NetworkClient.localPlayer.TryGetComponent(out PlayerInteractionHandler owned))
        {
            return owned;
        }

        return Object.FindAnyObjectByType<PlayerInteractionHandler>();
    }

    public static PlayerInteractionHandler ForIdentity(NetworkIdentity requester)
    {
        if (requester != null)
        {

            PlayerInteractionHandler handler = requester.GetComponentInChildren<PlayerInteractionHandler>(true);
            if (handler == null) handler = requester.GetComponentInParent<PlayerInteractionHandler>();
            if (handler != null) return handler;
        }

        return Local();
    }
}
