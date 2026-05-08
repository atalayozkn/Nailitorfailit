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

    [Header("Değişken")]
    [SyncVar] private uint carriedItemNetId = 0;

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

        // BASILI TUTMA -> sadece WorkStation work
        if (interactAction.action.IsPressed())
        {
            if (!IsCarrying && TryGetWorkStation(out WorkStation station))
            {
                station.RequestHoldWork();
            }
        }

        // TEK BASMA
        if (interactAction.action.WasPressedThisFrame())
        {
            // 1) Elde item varsa önce WorkStation kontrol et
            if (IsCarrying)
            {
                if (TryGetWorkStation(out WorkStation station))
                {
                    CmdPlaceToStation(currentNetObj.netId, station.netId);
                    return;
                }

                // 2) WorkStation değilse ama bir interact target varsa
                //    (duvar / zemin gibi) önce onu dene
                if (currentTarget != null)
                {
                    CmdInteract(currentTarget.netId);
                    return;
                }

                // 3) Hiçbir target yoksa bırak
                Drop();
                return;
            }

            // 4) Elde item yoksa normal interact
            if (currentTarget != null)
            {
                CmdInteract(currentTarget.netId);
            }
        }
    }

    public IPickupable GetCurrentItem()
    {
        return currentCarriable;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastDropTime < dropCooldown)
            return;

        NetworkIdentity id = other.GetComponentInParent<NetworkIdentity>();
        if (id == null) return;

        if (!id.CompareTag("Interactable") && !id.CompareTag("Pickup"))
            return;

        currentTarget = id;
        Debug.Log("Target girildi: " + id.name);
    }

    private void OnTriggerExit(Collider other)
    {
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
            station = hit.GetComponentInParent<WorkStation>();
            if (station != null)
            {
                return true;
            }
        }

        return false;
    }

    private void Drop()
    {
        if (!IsCarrying) return;

        CmdClearCarriedItem();

        currentCarriable.OnDrop();

        carriedRb.isKinematic = false;

        Physics.IgnoreCollision(
            GetComponentInParent<Collider>(),
            carriedRb.GetComponent<Collider>(),
            false
        );

        currentTarget = null;
        currentCarriable = null;
        currentNetObj = null;
        carriedRb = null;

        lastDropTime = Time.time;

        Debug.Log("Item bırakıldı");
    }

    [Command]
    private void CmdClearCarriedItem()
    {
        carriedItemNetId = 0;
    }

    [TargetRpc]
    private void TargetPickup(NetworkConnection target, NetworkIdentity id)
    {
        var item = id.GetComponent<IPickupable>();
        if (item == null) return;

        currentTarget = null;
        currentCarriable = item;
        currentNetObj = id;
        carriedRb = item.GetRigidbody();

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

        int recipeIndex = station.GetRecipeIndexForMaterial(carriable.Material);

        if (recipeIndex == -1)
        {
            Debug.Log("Bu materyal burada kullanılamaz");
            return;
        }

        station.CmdPlaceItem(itemId, recipeIndex);

        // EN KRİTİK SATIR
        carriedItemNetId = 0;

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

        currentTarget = null;
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

        // PICKUP
        if (id.CompareTag("Pickup"))
        {
            if (carriedItemNetId != 0) return;

            var item = id.GetComponent<IPickupable>();
            if (item != null)
            {
                carriedItemNetId = id.netId;
                TargetPickup(connectionToClient, id);
                return;
            }
        }

        // CONSTRUCT
        if (id.TryGetComponent<ItemScript.ConstructObject>(out var construct))
        {
            if (carriedItemNetId == 0) return;
            if (!NetworkServer.spawned.TryGetValue(carriedItemNetId, out NetworkIdentity carriedId)) return;

            var carriedObj = carriedId.GetComponent<CarriableObject>();
            if (carriedObj == null) return;

            bool built = construct.TryBuild(carriedObj);
            if (!built) return;

            NetworkServer.Destroy(carriedId.gameObject);
            carriedItemNetId = 0;
            TargetReleaseItem(connectionToClient);
            return;
        }

        // NORMAL INTERACT
        var interactable = id.GetComponent<IInteractable>();
        interactable?.Interact();
    }
}