using System.Collections;
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

    private Renderer highlightedRenderer;
    private Material highlightedMaterial;
    private Color highlightedOriginalColor;

    [Header("DEBUG")]
    [SerializeField] private NetworkIdentity currentTarget;

    private Coroutine holdWorkRoutine;
    private bool interactHeld;

    private void FixedUpdate()
    {
        if (!IsCarrying || carriedRb == null) return;

        carriedRb.MovePosition(holdPoint.position);
        carriedRb.MoveRotation(holdPoint.rotation);
    }

    private void Update()
    {
        if (!isOwned) return;
        if (interactAction == null || interactAction.action == null) return;

        if (interactAction.action.WasPressedThisFrame())
        {
            interactHeld = true;

            HandleInteractPressed();

            if (!IsCarrying && holdWorkRoutine == null)
            {
                holdWorkRoutine = StartCoroutine(HoldWorkRoutine());
            }
        }

        if (interactAction.action.WasReleasedThisFrame())
        {
            interactHeld = false;

            if (holdWorkRoutine != null)
            {
                StopCoroutine(holdWorkRoutine);
                holdWorkRoutine = null;
            }

            if (TryGetWorkStation(out WorkStation station))
            {
                station.RequestStopWork();
            }
        }
    }

    private IEnumerator HoldWorkRoutine()
    {
        while (interactHeld)
        {
            if (!IsCarrying && TryGetWorkStation(out WorkStation station))
            {
                station.RequestHoldWork();
            }

            yield return null;
        }

        holdWorkRoutine = null;
    }

    private void HandleInteractPressed()
    {
        if (IsCarrying)
        {
            if (TryGetWorkStation(out WorkStation station))
            {
                uint itemNetId = currentNetObj.netId;

                ReleaseHeldItemForStation();
                CmdPlaceToStation(itemNetId, station.netId);
                return;
            }

            if (currentTarget != null)
            {
                CmdInteract(currentTarget.netId);
                return;
            }

            Drop();
            return;
        }

        if (currentTarget != null)
        {
            CmdInteract(currentTarget.netId);
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

        SetCurrentTarget(id);

        Debug.Log("Target girildi: " + id.name);
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkIdentity id = other.GetComponentInParent<NetworkIdentity>();

        if (id != null && id == currentTarget)
        {
            ClearCurrentTarget();
            Debug.Log("Target çıkıldı");
        }
    }

    private void SetCurrentTarget(NetworkIdentity newTarget)
    {
        if (currentTarget == newTarget)
            return;

        RestoreHighlight();

        currentTarget = newTarget;

        ApplyHighlight(currentTarget);
    }

    private void ClearCurrentTarget()
    {
        RestoreHighlight();
        currentTarget = null;
    }

    private void ApplyHighlight(NetworkIdentity target)
    {
        if (target == null) return;

        highlightedRenderer = target.GetComponent<Renderer>();

        if (highlightedRenderer == null)
        {
            highlightedRenderer = target.GetComponentInChildren<Renderer>(true);
        }

        if (highlightedRenderer == null) return;

        Material[] mats = highlightedRenderer.materials;
        if (mats == null || mats.Length == 0) return;

        highlightedMaterial = mats[0];
        highlightedOriginalColor = highlightedMaterial.color;

        highlightedMaterial.color = Color.Lerp(
            highlightedOriginalColor,
            Color.red,
            0.9f
        );
    }

    private void RestoreHighlight()
    {
        if (highlightedMaterial != null)
        {
            highlightedMaterial.color = highlightedOriginalColor;
        }

        highlightedRenderer = null;
        highlightedMaterial = null;
    }

    private bool TryGetWorkStation(out WorkStation station)
    {
        station = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);

        foreach (var hit in hits)
        {
            station = hit.GetComponentInParent<WorkStation>();
            if (station != null)
                return true;
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

        ClearCurrentTarget();

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

        ClearCurrentTarget();

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

        ClearCurrentTarget();

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

        var interactable = id.GetComponent<IInteractable>();
        interactable?.Interact();
    }

    private void ReleaseHeldItemForStation()
    {
        if (!IsCarrying) return;

        if (carriedRb != null)
        {
            Physics.IgnoreCollision(
                GetComponentInParent<Collider>(),
                carriedRb.GetComponent<Collider>(),
                false
            );
        }

        ClearCurrentTarget();

        carriedRb = null;
        currentCarriable = null;
        currentNetObj = null;
    }

    private void OnDisable()
    {
        RestoreHighlight();
    }
}