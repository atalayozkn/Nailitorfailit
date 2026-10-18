using Mirror;

/// <summary>
/// The shelf purchase (buy a tool from the shop) is decided on the host: the host
/// spawns the bought item into the network and the client-side wallet pays. The
/// affordability answer comes from the PRESSING machine - the wallet in this build
/// is a shared one (CurrencyManager_MP ripples every change to everyone), so
/// trusting the requester's own view of it is consistent.
/// </summary>
public class ShopIndex_MP : NetworkBehaviour
{
    private ShopIndex shopIndex;

    private void Awake()
    {
        shopIndex = GetComponent<ShopIndex>();
    }

    public void RequestBuy()
    {
        if (isServer) ServerSpawnPurchase();
        else CmdRequestBuy();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestBuy() => ServerSpawnPurchase();

    [Server]
    private void ServerSpawnPurchase()
    {
        shopIndex.ServerBuy();
    }
}
