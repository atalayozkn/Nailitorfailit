
using Interactions.Networking;
using ItemScript;
using Mirror;

public class Constructor_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Constructor constructor;

    private void Awake()
    {
        constructor = GetComponent<Constructor>();
    }

    public void RequestInteract() => RequestBuild();

    public void RequestBuild()
    {
        if (isServer) ServerHandleBuild();
        else CmdRequestBuild();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestBuild() => ServerHandleBuild();

    [Server]
    private void ServerHandleBuild()
    {
        if (constructor.IsBuilt()) return;
        RpcConfirmBuild();
    }

    [ClientRpc]
    private void RpcConfirmBuild() => constructor.OnInteract();
}
