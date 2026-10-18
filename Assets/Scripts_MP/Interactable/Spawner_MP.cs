using Interactions.Networking;
using Mirror;
using UnityEngine;

public class Spawner_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private ObjectSpawner spawner;

    private void Awake()
    {
        spawner = GetComponent<ObjectSpawner>();
    }

    public void RequestInteract(NetworkIdentity requester, UnityEngine.Component interactable)
    {
        if (netIdentity != null)
        {
            if (isServer) ServerSpawn(requester);
            else CmdRequestSpawn(requester);
            return;
        }

        if (NetworkServer.active)
        {
            ServerSpawn(requester);
            return;
        }

        Debug.LogWarning($"[MP] Spawner_MP on '{name}' has no NetworkIdentity; " +
                         "the interaction was ignored on this client.", this);
    }

    public void RequestSpawn() => RequestInteract(null, null);

    [Command(requiresAuthority = false)]
    private void CmdRequestSpawn(NetworkIdentity requester) => ServerSpawn(requester);

    [Server]
    private void ServerSpawn(NetworkIdentity requester)
    {
        if (spawner == null)
        {
            Debug.LogError($"[MP] Spawner_MP on '{name}' has no ObjectSpawner component.", this);
            return;
        }

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        bool requesterCarrying = handler != null && handler.IsCarrying();

        InteractionDebug.Log($"Spawner_MP '{name}': request from {requester?.name ?? "host"} " +
                             $"handler={(handler != null)} carrying={requesterCarrying} " +
                             $"-> spawning={(handler == null || !requesterCarrying)}");

        spawner.SpawnForRemote(requesterCarrying);
    }
}
