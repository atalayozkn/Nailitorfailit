using Mirror;
using UnityEngine;

public static class PlayerInteractionResolver
{
    public static PlayerInteractionHandler Local()
    {
        if (NetworkClient.active && NetworkClient.localPlayer != null)
        {
            PlayerInteractionHandler owned =
                NetworkClient.localPlayer.GetComponentInChildren<PlayerInteractionHandler>(true);

            if (owned != null) return owned;
        }

        if (NetworkServer.active && NetworkClient.localPlayer != null)
        {
            PlayerInteractionHandler host =
                NetworkClient.localPlayer.GetComponentInChildren<PlayerInteractionHandler>(true);

            if (host != null) return host;
        }

        return Object.FindAnyObjectByType<PlayerInteractionHandler>(FindObjectsInactive.Include);
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
