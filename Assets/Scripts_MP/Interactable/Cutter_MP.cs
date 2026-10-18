using Interactions.Networking;
using Mirror;
using UnityEngine;

/// <summary>
/// Station-root proxy. The station prefab contains several interactables - the
/// cutter itself, and its fuel generator (Generator_Prototype) - so the proxy has to
/// know which one the player looked at. It also resolves the carried state from the
/// REQUESTER's mirrored copy on the host.
/// </summary>
public class Cutter_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Cutter cutter;
    private Generator_Prototype generator;

    private void Awake()
    {
        cutter = GetComponent<Cutter>();
        generator = GetComponentInChildren<Generator_Prototype>(true);
    }

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        // Component references cannot travel in a Command; decide here and send the answer.
        bool usedGenerator = interactable is Generator_Prototype;

        if (isServer) ServerHandle(requester, usedGenerator);
        else CmdRequestInteract(requester, usedGenerator);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester, bool usedGenerator) =>
        ServerHandle(requester, usedGenerator);

    [Server]
    private void ServerHandle(NetworkIdentity requester, bool usedGenerator)
    {
        InteractionDebug.Log($"Cutter_MP server handle: requester={requester?.name ?? "host"} " +
                             $"generator={usedGenerator} cutter={(cutter != null)}");

        if (usedGenerator)
        {
            if (generator == null)
            {
                Debug.LogError("Cutter_MP: no Generator_Prototype found in children.", this);
                return;
            }

            generator.ServerInteract(requester);
            return;
        }

        if (cutter == null)
        {
            Debug.LogError("Cutter_MP: no Cutter found on this object.", this);
            return;
        }

        cutter.ServerInteract(requester);
    }
}
