using Interactions.Networking;
using Mirror;
using UnityEngine;

/// <summary>
/// The in-level shelf vendor (Shop_Oil / Shop_EnergyDrink / Shop_PetFood shelves).
///
/// Those shelves only had a highlight helper: in the MP level there was NO way to
/// obtain oil / energy drink / pet food at all. This proxy turns each shelf into a
/// networked purchase point: the HOST decides (requester's hands, wallet), spawns the
/// item into the network and hands it to the requester on every machine.
/// </summary>
public class Shop_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Shop shop;

    private void Awake()
    {
        shop = GetComponent<Shop>();
    }

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        if (isServer) ServerHandle(requester);
        else CmdRequestInteract(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester) => ServerHandle(requester);

    [Server]
    private void ServerHandle(NetworkIdentity requester)
    {
        if (shop == null)
        {
            Debug.LogError("Shop_MP: no Shop component on this object.", this);
            return;
        }

        InteractionDebug.Log($"Shop_MP server handling purchase for " +
                             $"{requester?.name ?? "host"} on '{name}'");

        shop.ServerInteract(requester);
    }
}
