using System.Collections;
using Interactions;
using ItemScript;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract : NetworkBehaviour
{
    [SerializeField] private InputActionReference interactAction;

    [Header("Detection")]
    [SerializeField] private Transform detectionOrigin;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private float detectionInterval = 0.05f;

    [Header("Forward Detection")]
    [SerializeField] private float forwardCheckDistance = 1.4f;
    [SerializeField] private float forwardCheckRadius = 0.25f;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckForwardOffset = 0.65f;
    [SerializeField] private float groundCheckHeight = 0.8f;
    [SerializeField] private float groundCheckDistance = 1.3f;
    [SerializeField] private float groundCheckRadius = 0.35f;

    [Header("Carry")]
    private Rigidbody carriedRb;
    private Collider carriedCollider;
    private IPickupable currentCarriable;
    private NetworkIdentity currentNetObj;

    [SerializeField] private Transform holdPoint;
    public bool IsCarrying => currentCarriable != null;

    [Header("Değişken")]
    [SyncVar] private uint carriedItemNetId = 0;

    [Header("Cooldown")]
    [SerializeField] private float dropCooldown = 0.2f;
    private float lastDropTime;

    [Header("Highlight")]
    [SerializeField] private float highlightRedAmount = 0.9f;

    private Renderer highlightedRenderer;
    private Material highlightedMaterial;
    private Color highlightedOriginalColor;

    [Header("DEBUG")]
    [SerializeField] private NetworkIdentity currentTarget;

    private Collider playerCollider;

    private Coroutine holdWorkRoutine;
    private Coroutine targetDetectionRoutine;
    private bool interactHeld;

    private readonly RaycastHit[] detectionHits = new RaycastHit[12];

    private void Awake()
    {
        playerCollider = GetComponentInParent<Collider>();

        if (detectionOrigin == null)
            detectionOrigin = transform;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isOwned) return;

        if (interactAction != null)
            interactAction.action.Enable();

        StartTargetDetectionRoutine();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (!isOwned) return;

        if (interactAction != null)
            interactAction.action.Disable();

        StopHoldWorkRoutine();
        StopTargetDetectionRoutine();
        RestoreHighlight();
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
                holdWorkRoutine = StartCoroutine(HoldWorkRoutine());
        }

        if (interactAction.action.WasReleasedThisFrame())
        {
            interactHeld = false;

            StopHoldWorkRoutine();

            if (TryGetCurrentTargetStation(out WorkStation station))
                station.RequestStopWork();
        }
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;
        if (!IsCarrying || carriedRb == null) return;

        carriedRb.MovePosition(holdPoint.position);
        carriedRb.MoveRotation(holdPoint.rotation);
    }

    private void StartTargetDetectionRoutine()
    {
        if (targetDetectionRoutine != null)
            return;

        targetDetectionRoutine = StartCoroutine(TargetDetectionRoutine());
    }

    private void StopTargetDetectionRoutine()
    {
        if (targetDetectionRoutine != null)
        {
            StopCoroutine(targetDetectionRoutine);
            targetDetectionRoutine = null;
        }
    }

    private IEnumerator TargetDetectionRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(detectionInterval);

        while (true)
        {
            RefreshCurrentTarget();
            yield return wait;
        }
    }

    private void RefreshCurrentTarget()
    {
        NetworkIdentity forwardTarget = FindForwardTarget();
        NetworkIdentity groundTarget = FindGroundTarget();

        NetworkIdentity bestTarget = forwardTarget != null ? forwardTarget : groundTarget;

        SetCurrentTarget(bestTarget);
    }

    private NetworkIdentity FindForwardTarget()
    {
        Vector3 origin = detectionOrigin.position;
        Vector3 direction = detectionOrigin.forward;

        int count = Physics.SphereCastNonAlloc(
            origin,
            forwardCheckRadius,
            direction,
            detectionHits,
            forwardCheckDistance,
            interactableMask,
            QueryTriggerInteraction.Collide
        );

        return GetBestTargetFromHits(count);
    }

    private NetworkIdentity FindGroundTarget()
    {
        Vector3 origin =
            transform.position +
            transform.forward * groundCheckForwardOffset +
            Vector3.up * groundCheckHeight;

        int count = Physics.SphereCastNonAlloc(
            origin,
            groundCheckRadius,
            Vector3.down,
            detectionHits,
            groundCheckDistance,
            interactableMask,
            QueryTriggerInteraction.Collide
        );

        return GetBestTargetFromHits(count);
    }

    private NetworkIdentity GetBestTargetFromHits(int hitCount)
    {
        NetworkIdentity bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = detectionHits[i].collider;
            if (hitCollider == null) continue;

            NetworkIdentity possibleTarget = GetValidTargetFromCollider(hitCollider);
            if (possibleTarget == null) continue;

            float hitDistance = detectionHits[i].distance;

            if (hitDistance < bestDistance)
            {
                bestDistance = hitDistance;
                bestTarget = possibleTarget;
            }
        }

        return bestTarget;
    }

    private NetworkIdentity GetValidTargetFromCollider(Collider hitCollider)
    {
        NetworkIdentity id = hitCollider.GetComponentInParent<NetworkIdentity>();
        if (id == null) return null;

        if (currentNetObj != null && id == currentNetObj)
            return null;

        bool isPickup = id.CompareTag("Pickup") || id.GetComponent<IPickupable>() != null;
        bool isInteractable = id.CompareTag("Interactable") || id.GetComponent<IInteractable>() != null;

        if (!isInteractable && !isPickup)
            return null;

        if (IsCarrying)
        {
            if (isPickup)
                return null;

            if (TryGetStationFromTarget(id, out WorkStation station))
            {
                if (CanPlaceCurrentItemToStation(station))
                    return id;

                return null;
            }

            if (id.TryGetComponent<ConstructObject>(out ConstructObject construct))
            {
                if (CanBuildCurrentItemToConstruct(construct))
                    return id;

                return null;
            }

            return null;
        }

        if (Time.time - lastDropTime < dropCooldown && isPickup)
            return null;

        return id;
    }

    private bool CanPlaceCurrentItemToStation(WorkStation station)
    {
        if (station == null) return false;
        if (currentNetObj == null) return false;

        CarriableObject carriable = currentNetObj.GetComponent<CarriableObject>();
        if (carriable == null) return false;

        return station.GetRecipeIndexForMaterial(carriable.Material) != -1;
    }

    private bool CanBuildCurrentItemToConstruct(ConstructObject construct)
    {
        if (construct == null) return false;
        if (currentNetObj == null) return false;

        CarriableObject carriable = currentNetObj.GetComponent<CarriableObject>();
        if (carriable == null) return false;

        return construct.CanBuildWith(carriable);
    }

    private IEnumerator HoldWorkRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.05f);

        while (interactHeld)
        {
            if (!IsCarrying && TryGetCurrentTargetStation(out WorkStation station))
                station.RequestHoldWork();

            yield return wait;
        }

        holdWorkRoutine = null;
    }

    private void StopHoldWorkRoutine()
    {
        if (holdWorkRoutine != null)
        {
            StopCoroutine(holdWorkRoutine);
            holdWorkRoutine = null;
        }
    }

    private void HandleInteractPressed()
    {
        if (IsCarrying)
        {
            if (TryGetCurrentTargetStation(out WorkStation station))
            {
                if (currentNetObj == null) return;

                CmdPlaceToStation(currentNetObj.netId, station.netId);
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

    private bool TryGetCurrentTargetStation(out WorkStation station)
    {
        station = null;

        if (currentTarget == null)
            return false;

        return TryGetStationFromTarget(currentTarget, out station);
    }

    private bool TryGetStationFromTarget(NetworkIdentity target, out WorkStation station)
    {
        station = null;

        if (target == null)
            return false;

        station = target.GetComponent<WorkStation>();

        if (station == null)
            station = target.GetComponentInChildren<WorkStation>(true);

        return station != null;
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
            highlightedRenderer = target.GetComponentInChildren<Renderer>(true);

        if (highlightedRenderer == null) return;

        Material[] mats = highlightedRenderer.materials;
        if (mats == null || mats.Length == 0) return;

        highlightedMaterial = mats[0];
        highlightedOriginalColor = highlightedMaterial.color;

        highlightedMaterial.color = Color.Lerp(
            highlightedOriginalColor,
            Color.red,
            highlightRedAmount
        );
    }

    private void RestoreHighlight()
    {
        if (highlightedMaterial != null)
            highlightedMaterial.color = highlightedOriginalColor;

        highlightedRenderer = null;
        highlightedMaterial = null;
    }

    private void Drop()
    {
        if (!IsCarrying) return;

        uint itemNetId = currentNetObj != null ? currentNetObj.netId : 0;

        if (itemNetId != 0)
            CmdDropHeldItem(itemNetId);

        if (currentCarriable != null)
            currentCarriable.OnDrop();

        if (carriedRb != null)
            carriedRb.isKinematic = false;

        SetHeldItemCollision(false);

        ClearCurrentTarget();
        ClearLocalCarryReferences();

        lastDropTime = Time.time;

        Debug.Log("Item bırakıldı");
    }

    [Command]
    private void CmdDropHeldItem(uint itemNetId)
    {
        if (carriedItemNetId != itemNetId)
            return;

        carriedItemNetId = 0;
    }

    [TargetRpc]
    private void TargetPickup(NetworkConnection target, NetworkIdentity id)
    {
        if (id == null) return;

        var item = id.GetComponent<IPickupable>();
        if (item == null) return;

        Rigidbody itemRb = item.GetRigidbody();
        if (itemRb == null) return;

        ClearCurrentTarget();

        currentCarriable = item;
        currentNetObj = id;
        carriedRb = itemRb;
        carriedCollider = carriedRb.GetComponent<Collider>();

        carriedRb.transform.SetParent(null);

        currentCarriable.OnPickUp();
        carriedRb.isKinematic = true;

        carriedRb.transform.position = holdPoint.position;
        carriedRb.transform.rotation = holdPoint.rotation;

        SetHeldItemCollision(true);

        Debug.Log("Item alındı: " + id.name);
    }

    [Command]
    private void CmdPlaceToStation(uint itemId, uint stationId)
    {
        if (carriedItemNetId != itemId)
            return;

        if (!NetworkServer.spawned.TryGetValue(itemId, out NetworkIdentity item))
            return;

        if (!NetworkServer.spawned.TryGetValue(stationId, out NetworkIdentity stationObj))
            return;

        WorkStation station = stationObj.GetComponent<WorkStation>();
        CarriableObject carriable = item.GetComponent<CarriableObject>();

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
        SetHeldItemCollision(false);

        ClearCurrentTarget();
        ClearLocalCarryReferences();

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

            IPickupable item = id.GetComponent<IPickupable>();

            if (item != null)
            {
                carriedItemNetId = id.netId;
                TargetPickup(connectionToClient, id);
                return;
            }
        }

        if (id.TryGetComponent<ConstructObject>(out ConstructObject construct))
        {
            if (carriedItemNetId == 0) return;

            if (!NetworkServer.spawned.TryGetValue(carriedItemNetId, out NetworkIdentity carriedId))
                return;

            CarriableObject carriedObj = carriedId.GetComponent<CarriableObject>();
            if (carriedObj == null) return;

            bool built = construct.TryBuild(carriedObj);
            if (!built) return;

            NetworkServer.Destroy(carriedId.gameObject);

            carriedItemNetId = 0;

            TargetReleaseItem(connectionToClient);

            return;
        }

        IInteractable interactable = id.GetComponent<IInteractable>();
        interactable?.Interact();
    }

    private void SetHeldItemCollision(bool ignorePlayerCollision)
    {
        if (playerCollider == null)
            playerCollider = GetComponentInParent<Collider>();

        if (playerCollider == null || carriedCollider == null)
            return;

        Physics.IgnoreCollision(
            playerCollider,
            carriedCollider,
            ignorePlayerCollision
        );
    }

    private void ClearLocalCarryReferences()
    {
        carriedRb = null;
        carriedCollider = null;
        currentCarriable = null;
        currentNetObj = null;
    }

    private void OnDisable()
    {
        StopHoldWorkRoutine();
        StopTargetDetectionRoutine();
        RestoreHighlight();
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform = detectionOrigin != null ? detectionOrigin : transform;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            originTransform.position,
            originTransform.position + originTransform.forward * forwardCheckDistance
        );

        Vector3 groundOrigin =
            transform.position +
            transform.forward * groundCheckForwardOffset +
            Vector3.up * groundCheckHeight;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            groundOrigin,
            groundOrigin + Vector3.down * groundCheckDistance
        );
    }
}