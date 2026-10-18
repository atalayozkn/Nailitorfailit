using System.Collections;
using ItemScript;
using Mirror;
using UnityEngine;

/// <summary>
/// Networked half of a carriable object.
///
/// Every state change of a carriable must be applied on EVERY machine - the SP code
/// alone only mutates the local copy, which is why items stayed in the player's hand
/// on the other clients, why a consumed item kept existing for them, and why their
/// "is carrying" flag got stuck.
///
/// Operations (all server-authoritative, applied everywhere by ClientRpc):
///   - pickup / drop                      (already existed)
///   - place into a station slot          (cutter, mixer)
///   - consume (fuel, ingredients, build) (destroys the networked object properly)
///   - drop back out of a station         (wrong mixer formula)
/// </summary>
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

    // ---------------------------------------------------------------- pickup / drop

    public void NotifyPickedUp(PlayerInteractionHandler interactor)
    {
        // The handler lives on a child of the networked player object, so the
        // identity must be searched upwards - with GetComponent it was null and
        // every pickup silently aborted.
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

    // ---------------------------------------------------------------- station: place

    /// <summary>
    /// Server: put this carriable into a station slot (cutter/mixer). Applied on every
    /// machine, so the item visibly leaves the requester's hand everywhere - and each
    /// machine clears the requester's carried state on ITS copy of that player.
    /// </summary>
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

        if (station != null && station.TryGetComponent(out IStationPlacement placement))
            placementTransform = placement.GetPlacementTransform();

        if (placementTransform != null)
        {
            transform.SetParent(placementTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

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

    // ---------------------------------------------------------------- station: consume

    /// <summary>
    /// Server: the carriable is being used up (generator fuel, ingredients, construction
    /// material). The bookkeeping runs everywhere (counters, SFX, carried state) while
    /// only the server destroys the networked object - clients get the removal from
    /// Mirror, so they never keep a ghost item around.
    /// </summary>
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
        // The server copy already did its bookkeeping before the RPC.
        if (!isServer && carriableSp != null) carriableSp.MarkConsumedForNetwork();

        ClearRequesterCarriedState(requester);
    }

    /// <summary>
    /// Server: hand this freshly spawned item straight to the requester (shop
    /// purchase). Applied everywhere, so the item is in that player's hands on every
    /// machine.
    /// </summary>
    [Server]
    public void ServerGiveTo(NetworkIdentity requester)
    {
        RpcApplyPickup(requester);
    }

    /// <summary>
    /// Owner-side entry point (called on the machine that interacted, e.g. the dog's
    /// owner feeding the bowl): consume the item through the network.
    /// </summary>
    public void RequestConsume(NetworkIdentity requester)
    {
        if (isServer) ServerConsumeNetworked(requester);
        else CmdRequestConsumeNetworked(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestConsumeNetworked(NetworkIdentity requester) => ServerConsumeNetworked(requester);

    // ---------------------------------------------------------------- station: drop back

    /// <summary>Server: a station spits the item back out (e.g. wrong mixer formula).</summary>
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

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Clears the carried state on the requester's copy ON THIS MACHINE. On the host
    /// that is the real player object; on clients it is that player's ghost - keeping
    /// every machine's view of "who is carrying what" identical.
    /// </summary>
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
