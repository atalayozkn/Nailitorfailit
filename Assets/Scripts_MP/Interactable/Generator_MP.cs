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
    private void CmdRequestInteract(NetworkIdentity requester) => ServerHandle(requester);

    [Server]
    private void ServerHandle(NetworkIdentity requester) => generator.ServerInteract(requester);
}
