using UnityEngine;
using Interactions;
using PlayerScripts;
using Mirror;

public class EnergyDrink : NetworkBehaviour, IInteractable
{
    public void Interact()
    {
        var player = NetworkClient.connection.identity.GetComponent<PlayerMove>();

        if (player != null)
        {
            player.RefillEnergy();
            CmdConsume();
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdConsume()
    {
        NetworkServer.Destroy(gameObject);
    }
}