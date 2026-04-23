using Interactions;
using ItemScript;
using Mirror;
using PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract : NetworkBehaviour
{
    [SerializeField] private InputActionReference interactAction;

    [Header("Carry")]
    private Rigidbody carriedRb;
    private IPickupable currentCarriable;
    private NetworkIdentity currentNetObj;
    [SerializeField] private Transform holdPoint;
    public bool IsCarrying => currentCarriable != null;

    [Header("Cooldown")]
    private float dropCooldown = 0.2f;
    private float lastDropTime;

    [Header("DEBUG")]
    [SerializeField] private NetworkIdentity currentTarget;

    void FixedUpdate()
    {
        if (!IsCarrying || carriedRb == null) return;

        carriedRb.MovePosition(holdPoint.position);
        carriedRb.MoveRotation(holdPoint.rotation);
    }

    void Update()
    {
        if (!isOwned) return;

        if (interactAction == null || interactAction.action == null)
            return;

        // 🔥 BASILI TUTMA (WORK)
        if (interactAction.action.IsPressed())
        {
            if (!IsCarrying && TryGetWorkStation(out WorkStation station))
            {
                station.RequestHoldWork();
            }
        }

        // 🔥 TEK BASMA (E CLICK)
        if (interactAction.action.WasPressedThisFrame())
        {
            if (TryGetWorkStation(out WorkStation station))
            {
                if (IsCarrying)
                {
                    CmdPlaceToStation(currentNetObj.netId, station.netId);
                    return;
                }
            }

            if (IsCarrying)
            {
                Drop();
                return;
            }

            if (currentTarget != null)
            {
                CmdInteract(currentTarget.netId);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastDropTime < dropCooldown)
            return;

        if (!other.CompareTag("Interactable") && !other.CompareTag("Pickup"))
            return;

        NetworkIdentity id = other.GetComponentInParent<NetworkIdentity>();

        if (id != null)
        {
            currentTarget = id;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //if (!other.CompareTag("Interactable"))
        //    return;

        NetworkIdentity id = other.GetComponentInParent<NetworkIdentity>();

        if (id != null && id == currentTarget)
        {
            currentTarget = null;
            Debug.Log("Target çıkıldı");
        }
    }

    private bool TryGetWorkStation(out WorkStation station)
    {
        station = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<WorkStation>(out station))
            {
                return true;
            }
        }

        return false;
    }

    private void Drop()
    {
        if (!IsCarrying) return;

        currentCarriable.OnDrop();

        carriedRb.isKinematic = false;

        Physics.IgnoreCollision(
            GetComponentInParent<Collider>(),
            carriedRb.GetComponent<Collider>(),
            false
        );

        // 🔥 EN KRİTİK SATIR
        currentTarget = null;

        currentCarriable = null;
        currentNetObj = null;
        carriedRb = null;

        lastDropTime = Time.time;
        currentTarget = null;

        Debug.Log("Item bırakıldı");
    }

    [TargetRpc]
    private void TargetPickup(NetworkConnection target, NetworkIdentity id)
    {
        var item = id.GetComponent<IPickupable>();
        if (item == null) return;

        currentCarriable = item;
        currentNetObj = id;
        carriedRb = item.GetRigidbody();

        // 🔥 Eski parent'ı kopar
        carriedRb.transform.SetParent(null);

        currentCarriable.OnPickUp();
        carriedRb.isKinematic = true;

        carriedRb.transform.position = holdPoint.position;
        carriedRb.transform.rotation = holdPoint.rotation;

        Physics.IgnoreCollision(
            GetComponentInParent<Collider>(),
            carriedRb.GetComponent<Collider>(),
            true
        );
    }

    [Command]
    private void CmdPlaceToStation(uint itemId, uint stationId)
    {
        if (!NetworkServer.spawned.TryGetValue(itemId, out NetworkIdentity item))
            return;

        if (!NetworkServer.spawned.TryGetValue(stationId, out NetworkIdentity stationObj))
            return;

        var station = stationObj.GetComponent<WorkStation>();
        var carriable = item.GetComponent<CarriableObject>();

        if (station == null || carriable == null)
            return;

        // 🎯 MATERIAL KONTROL
        int recipeIndex = station.GetRecipeIndexForMaterial(carriable.Material);

        if (recipeIndex == -1)
        {
            Debug.Log("Bu materyal burada kullanılamaz");
            return;
        }

        // 🔥 WORKSTATION METHODU
        station.CmdPlaceItem(itemId, recipeIndex);

        TargetReleaseItem(connectionToClient);
    }

    [TargetRpc]
    private void TargetReleaseItem(NetworkConnection target)
    {
        if (carriedRb != null)
        {
            Physics.IgnoreCollision(
                GetComponentInParent<Collider>(),
                carriedRb.GetComponent<Collider>(),
                false
            );
        }

        carriedRb = null;
        currentCarriable = null;
        currentNetObj = null;

        Debug.Log("Item artık player'da değil");
    }

    [Command]
    private void CmdInteract(uint netId)
    {
        if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity id))
            return;

        // 🎯 PICKUP
        if (id.CompareTag("Pickup"))
        {
            var item = id.GetComponent<IPickupable>();
            if (item != null)
            {
                TargetPickup(connectionToClient, id);
                return;
            }
        }

        // 🎯 NORMAL INTERACT
        var interactable = id.GetComponent<IInteractable>();
        interactable?.Interact();
    }
}