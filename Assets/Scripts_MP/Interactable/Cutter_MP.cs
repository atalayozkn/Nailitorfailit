
using Interactions.Networking;
using Mirror;

public class Cutter_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Cutter cutter;

    private void Awake()
    {
        cutter = GetComponent<Cutter>();
    }

    public void RequestInteract()
    {
        if (isServer) ServerHandle();
        else CmdRequestInteract();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract() => ServerHandle();

    [Server]
    private void ServerHandle() => cutter.OnInteract();
}
