
using Interactions.Networking;
using Mirror;

public class MixerHandle_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private MixerHandle mixerHandle;

    private void Awake()
    {
        mixerHandle = GetComponent<MixerHandle>();
    }

    public void RequestInteract()
    {
        if (isServer) ServerHandle();
        else CmdRequestInteract();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract() => ServerHandle();

    [Server]
    private void ServerHandle() => mixerHandle.OnInteract();
}
