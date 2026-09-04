
using Mirror;

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
    private void ServerSpawnPurchase() => shopIndex.Buy();
}
