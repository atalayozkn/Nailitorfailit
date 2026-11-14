using Interactions.Networking;
using Mirror;

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
    private void CmdRequestInteract(NetworkIdentity requester, NetworkConnectionToClient sender = null)
    {
        // Act for the sender's own player, never for an identity the client claims.
        if (sender != null && sender.identity != null) requester = sender.identity;
        ServerHandle(requester);
    }

    [Server]
    private void ServerHandle(NetworkIdentity requester) => generator.ServerInteract(requester);
}
