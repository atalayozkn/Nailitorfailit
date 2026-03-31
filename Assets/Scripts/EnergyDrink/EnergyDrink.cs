using UnityEngine;
using Interactions;
using PlayerScripts;
using Mirror;

public class EnergyDrink : NetworkBehaviour, IInteractable
{
    public bool Interact(IPickupable heldItem)
    {
        // El doluyken içemesin (opsiyonel)
        if (heldItem != null) return false;

        var player = NetworkClient.connection.identity.GetComponent<PlayerMove>();

        if (player != null)
        {
            player.RefillEnergy();
            CmdConsume();
            return true;
        }

        return false;
    }

    [Command(requiresAuthority = false)]
    private void CmdConsume()
    {
        NetworkServer.Destroy(gameObject);
    }
}