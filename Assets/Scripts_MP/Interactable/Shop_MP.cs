using Interactions.Networking;
using ItemScript;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Shop_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Shop shop;

    private void Awake()
    {
        shop = GetComponent<Shop>();
    }

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        if (isServer) ServerHandle(requester);
        else CmdRequestInteract(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester) => ServerHandle(requester);

    [Server]
    private void ServerHandle(NetworkIdentity requester)
    {
        if (shop == null)
        {
            Debug.LogError("Shop_MP: no Shop component on this object.", this);
            return;
        }

        InteractionDebug.Log($"Shop_MP server handling purchase for " +
                             $"{requester?.name ?? "host"} on '{name}'");

        GameObject prefab = MpSharedCompat.MpGetField<GameObject>(shop, "objectPrefab");
        if (prefab == null) return;

        int cost = MpSharedCompat.MpGetField(shop, "cost", 0);

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        if (handler != null && handler.IsCarrying()) return;
        if (CurrencyManager.Instance != null && !CurrencyManager.Instance.HasEnoughCurrency(cost)) return;

        Transform spawnTransform = MpSharedCompat.MpGetField<Transform>(shop, "spawnTransform");
        Vector3 position = spawnTransform != null ? spawnTransform.position : shop.transform.position;
        Quaternion rotation = spawnTransform != null ? spawnTransform.rotation : shop.transform.rotation;

        GameObject obj = Instantiate(MpPrefabResolver.Resolve(prefab), position, rotation);

        if (NetworkServer.active && obj.TryGetComponent(out NetworkIdentity identity))
            NetworkServer.Spawn(obj);

        if (obj.TryGetComponent(out CarriableObject_MP carriableMp))
            carriableMp.ServerGiveTo(requester);
        else if (obj.TryGetComponent(out CarriableObject_SP carriable))
            carriable.SnapToRightHand();

        CurrencyManager_MP.Instance?.RequestSpend(cost);

        UnityEvent onPurchase = MpSharedCompat.MpGetField<UnityEvent>(shop, "onPurchaseEvent");
        if (onPurchase != null) onPurchase.Invoke();
    }
}
