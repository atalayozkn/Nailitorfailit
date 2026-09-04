
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
        NetworkIdentity carrierIdentity = interactor.GetComponent<NetworkIdentity>();
        if (carrierIdentity == null) return;

        if (isServer) RpcApplyPickup(carrierIdentity);
        else CmdRequestPickup(carrierIdentity);
    }

    public void NotifyDropped(PlayerInteractionHandler interactor)
    {
        NetworkIdentity carrierIdentity = interactor.GetComponent<NetworkIdentity>();
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

    private void ApplyPickupLocally(NetworkIdentity carrierIdentity)
    {
        if (carrierIdentity == null) return;

        PlayerInteractionHandler carrierInteraction = carrierIdentity.GetComponent<PlayerInteractionHandler>();
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

        PlayerInteractionHandler carrierInteraction = carrierIdentity != null ? carrierIdentity.GetComponent<PlayerInteractionHandler>() : null;
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
