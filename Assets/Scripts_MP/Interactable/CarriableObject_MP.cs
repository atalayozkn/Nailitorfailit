using System.Collections;
using ItemScript;
using Mirror;
using UnityEngine;

public class CarriableObject_MP : NetworkBehaviour
{
    private Rigidbody rb;
    private Collider col;
    private CarriableObject_SP carriableSp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        carriableSp = GetComponent<CarriableObject_SP>();
    }

    public void NotifyPickedUp(PlayerInteractionHandler interactor)
    {

        NetworkIdentity carrierIdentity = interactor != null
            ? interactor.GetComponentInParent<NetworkIdentity>()
            : null;

        InteractionDebug.Log($"Carriable '{name}': pickup requested, carrier=" +
                             $"{(carrierIdentity != null ? carrierIdentity.name : "NULL (aborting!)")} " +
                             $"server={isServer}");

        if (carrierIdentity == null) return;

        if (isServer) RpcApplyPickup(carrierIdentity);
        else CmdRequestPickup(carrierIdentity);
    }

    public void NotifyDropped(PlayerInteractionHandler interactor)
    {
        NetworkIdentity carrierIdentity = interactor.GetComponentInParent<NetworkIdentity>();
        if (carrierIdentity == null) return;

        if (isServer) RpcApplyDrop(carrierIdentity);
        else CmdRequestDrop(carrierIdentity);
    }

    public void NotifyConsumed(float delay)
    {
        if (isServer) StartCoroutine(ServerDestroyAfterDelay(delay));
        else CmdRequestConsume(delay);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestPickup(NetworkIdentity carrierIdentity) => RpcApplyPickup(carrierIdentity);

    [Command(requiresAuthority = false)]
    private void CmdRequestDrop(NetworkIdentity carrierIdentity) => RpcApplyDrop(carrierIdentity);

    [Command(requiresAuthority = false)]
    private void CmdRequestConsume(float delay) => StartCoroutine(ServerDestroyAfterDelay(delay));

    [ClientRpc]
    private void RpcApplyPickup(NetworkIdentity carrierIdentity) => ApplyPickupLocally(carrierIdentity);

    [ClientRpc]
    private void RpcApplyDrop(NetworkIdentity carrierIdentity) => ApplyDropLocally(carrierIdentity);

    [Server]
    public void ServerPlaceInStation(NetworkIdentity station, NetworkIdentity requester, bool hideVisuals)
    {
        InteractionDebug.Log($"Carriable '{name}': placed into station " +
                             $"'{(station != null ? station.name : "NULL")}' for {requester?.name ?? "host"}");

        RpcApplyPlaceInStation(station, requester, hideVisuals);
    }

    [ClientRpc]
    private void RpcApplyPlaceInStation(NetworkIdentity station, NetworkIdentity requester, bool hideVisuals)
    {
        Transform placementTransform = station != null ? station.transform : null;

        if (station != null)
        {
            Transform configured = MpSharedCompat.GetPlacementTransform(station);
            if (configured != null) placementTransform = configured;
        }

        if (placementTransform != null)
        {
            transform.SetParent(placementTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        InteractionDebug.Log($"Carriable '{name}': place-in-station RPC calisti " +
                             $"({(isServer ? "server" : "client")}), station=" +
                             $"'{(station != null ? station.name : "NULL")}', parent=" +
                             $"'{(transform.parent != null ? transform.parent.name : "NULL")}'");

        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.Sleep();
        }

        if (carriableSp != null)
        {
            carriableSp.SetOccupied();
            carriableSp.SetVisuals(!hideVisuals);
        }

        ClearRequesterCarriedState(requester);
    }

    [Server]
    public void ServerConsumeNetworked(NetworkIdentity requester, float destroyDelay = 0f)
    {
        InteractionDebug.Log($"Carriable '{name}': consumed (networked) for {requester?.name ?? "host"}");

        if (carriableSp != null) carriableSp.MarkConsumedForNetwork();

        RpcApplyConsumed(requester);

        if (destroyDelay > 0f) StartCoroutine(ServerDestroyAfterDelay(destroyDelay));
        else NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcApplyConsumed(NetworkIdentity requester)
    {

        if (!isServer && carriableSp != null) carriableSp.MarkConsumedForNetwork();

        ClearRequesterCarriedState(requester);
    }

    [Server]
    public void ServerGiveTo(NetworkIdentity requester)
    {
        RpcApplyPickup(requester);
    }

    public void RequestConsume(NetworkIdentity requester)
    {
        if (isServer) ServerConsumeNetworked(requester);
        else CmdRequestConsumeNetworked(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestConsumeNetworked(NetworkIdentity requester) => ServerConsumeNetworked(requester);

    [Server]
    public void ServerDropFromStation()
    {
        RpcApplyDropFromStation();
    }

    [ClientRpc]
    private void RpcApplyDropFromStation()
    {
        if (rb != null)
        {
            rb.WakeUp();
            rb.isKinematic = false;
        }

        transform.SetParent(null);

        if (col != null) col.enabled = true;

        if (carriableSp != null) carriableSp.SetVisuals(true);
    }

    private static void ClearRequesterCarriedState(NetworkIdentity requester)
    {
        if (requester == null) return;

        PlayerInteractionHandler handler = requester.GetComponentInChildren<PlayerInteractionHandler>(true);
        if (handler == null) handler = requester.GetComponentInParent<PlayerInteractionHandler>();
        if (handler != null) handler.ClearCarriedObject();
    }

    private void ApplyPickupLocally(NetworkIdentity carrierIdentity)
    {
        InteractionDebug.Log($"Carriable '{name}': RpcApplyPickup applied on " +
                             $"{(isServer ? "server" : "client")} for " +
                             $"{(carrierIdentity != null ? carrierIdentity.name : "NULL")}");

        if (carrierIdentity == null) return;

        PlayerInteractionHandler carrierInteraction = carrierIdentity.GetComponentInChildren<PlayerInteractionHandler>(true);
        if (carrierInteraction == null) carrierInteraction = carrierIdentity.GetComponentInParent<PlayerInteractionHandler>();
        if (carrierInteraction == null) return;

        Transform carryTransform = carrierInteraction.GetCarryTransform();

        if (col != null) col.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.Sleep();
        }

        transform.SetParent(carryTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (carriableSp != null) carrierInteraction.RegisterCarriedObject(carriableSp);
    }

    private void ApplyDropLocally(NetworkIdentity carrierIdentity)
    {
        InteractionDebug.Log($"Carriable '{name}': drop RPC calisti ({(isServer ? "server" : "client")}), " +
                             $"birakan='{(carrierIdentity != null ? carrierIdentity.name : "NULL")}'");

        if (rb != null)
        {
            rb.WakeUp();
            rb.isKinematic = false;
        }

        transform.SetParent(null);

        if (col != null) col.enabled = true;

        PlayerInteractionHandler carrierInteraction = null;
        if (carrierIdentity != null)
        {
            carrierInteraction = carrierIdentity.GetComponentInChildren<PlayerInteractionHandler>(true);
            if (carrierInteraction == null) carrierInteraction = carrierIdentity.GetComponentInParent<PlayerInteractionHandler>();
        }
        carrierInteraction?.ClearCarriedObject();
    }

    [Server]
    private IEnumerator ServerDestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (gameObject != null)
            NetworkServer.Destroy(gameObject);
    }
}
