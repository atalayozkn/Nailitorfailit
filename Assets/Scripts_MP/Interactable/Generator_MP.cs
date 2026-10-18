using Interactions.Networking;
using Mirror;

/// <summary>
/// The generator's fuel-fill is decided on the host (server-authoritative):
/// previously the interaction was fanned out with a ClientRpc, which made EVERY
/// machine run the interaction against their own ghost handlers - a remote player's
/// oil became "everybody's oil". Not anymore: the host runs it against the
/// requester's mirrored handler only.
/// </summary>
public class Generator_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Generator_SP generator;

    private void Awake()
    {
        generator = GetComponent<Generator_SP>();
    }

    public void RequestInteract(NetworkIdentity requester, UnityEngine.Component interactable)
    {
        if (isServer) ServerHandle(requester);
        else CmdRequestInteract(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester) => ServerHandle(requester);

    [Server]
    private void ServerHandle(NetworkIdentity requester) => generator.ServerInteract(requester);
}
