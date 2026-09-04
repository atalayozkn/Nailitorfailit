
using Interactions.Networking;
using Mirror;

public class Generator_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Generator_SP generator;

    private void Awake()
    {
        generator = GetComponent<Generator_SP>();
    }

    public void RequestInteract()
    {
        if (isServer) RpcConfirmInteract();
        else CmdRequestInteract();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract() => RpcConfirmInteract();

    [ClientRpc]
    private void RpcConfirmInteract() => generator.OnInteract();
}
