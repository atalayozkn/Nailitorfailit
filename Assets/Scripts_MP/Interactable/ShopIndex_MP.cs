using Mirror;
using UnityEngine;

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
        if (shopIndex == null) return;

        GameObject prefab = MpSharedCompat.MpGetField<GameObject>(shopIndex, "itemPrefab");
        if (prefab == null) return;

        int cost = MpSharedCompat.MpGetField(shopIndex, "itemCost", 0);
        if (CurrencyManager.Instance != null && !CurrencyManager.Instance.HasEnoughCurrency(cost)) return;

        Transform spawn = MpSharedCompat.MpGetField<Transform>(shopIndex, "spawnPosition");
        Vector3 position = spawn != null ? spawn.position : shopIndex.transform.position;
        Quaternion rotation = spawn != null ? spawn.rotation : shopIndex.transform.rotation;

        GameObject obj = Instantiate(MpPrefabResolver.Resolve(prefab), position, rotation);
        obj.transform.SetParent(null);

        if (NetworkServer.active && obj.TryGetComponent(out NetworkIdentity identity))
            NetworkServer.Spawn(obj);

        CurrencyManager_MP.Instance?.RequestSpend(cost);
    }
}
