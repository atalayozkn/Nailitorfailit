using System.Collections;
using Interactions;
using ItemScript;
using PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract_SP : MonoBehaviour
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
    private GameObject currentObj;

    [SerializeField] private Transform holdPoint;
    public bool IsCarrying => currentCarriable != null;

    [Header("Cooldown")]
    [SerializeField] private float dropCooldown = 0.2f;
    private float lastDropTime;

    [Header("Highlight")]
    [SerializeField] private float highlightRedAmount = 0.9f;

    private Renderer highlightedRenderer;
    private Material highlightedMaterial;
    private Color highlightedOriginalColor;

    [Header("DEBUG")]
    [SerializeField] private GameObject currentTarget;

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

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Enable();

        StartTargetDetectionRoutine();
    }

    private void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Disable();

        StopHoldWorkRoutine();
        StopTargetDetectionRoutine();
        RestoreHighlight();
    }

    private void Update()
    {
        if (interactAction == null || interactAction.action == null)
            return;

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

            if (TryGetCurrentTargetStation(out WorkStation_SP station))
                station.RequestStopWork();
        }
    }

    private void FixedUpdate()
    {
        if (!IsCarrying || carriedRb == null) return;
        if (holdPoint == null) return;

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
        GameObject forwardTarget = FindForwardTarget();
        GameObject groundTarget = FindGroundTarget();

        GameObject bestTarget = forwardTarget != null ? forwardTarget : groundTarget;

        SetCurrentTarget(bestTarget);
    }

    private GameObject FindForwardTarget()
    {
        Vector3 origin = detectionOrigin.position;
        Vector3 direction = detectionOrigin.forward;

        int count = Physics.SphereCastNonAlloc(
            origin,
            forwardCheckRadius,
            direction,
            detectionHits,
            forwardCheckDistance,
            interactableMask
        );

        return GetBestTargetFromHits(count);
    }

    private GameObject FindGroundTarget()
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
            interactableMask
        );

        return GetBestTargetFromHits(count);
    }

    private GameObject GetBestTargetFromHits(int hitCount)
    {
        GameObject bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = detectionHits[i].collider;
            if (hitCollider == null) continue;

            GameObject possibleTarget = GetValidTargetFromCollider(hitCollider);
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

    private GameObject GetValidTargetFromCollider(Collider hitCollider)
    {
        if (hitCollider == null) return null;

        ConstructObject_SP construct = hitCollider.GetComponentInParent<ConstructObject_SP>();
        WorkStation_SP station = hitCollider.GetComponentInParent<WorkStation_SP>();
        CarriableObject_SP carriable = hitCollider.GetComponentInParent<CarriableObject_SP>();

        IInteractable interactable = hitCollider.GetComponentInParent<IInteractable>();

        GameObject target = null;

        if (construct != null)
            target = construct.gameObject;
        else if (station != null)
            target = station.gameObject;
        else if (carriable != null)
            target = carriable.gameObject;
        else if (interactable is MonoBehaviour mb)
            target = mb.gameObject;

        if (target == null)
            return null;

        if (currentObj != null && target == currentObj)
            return null;

        bool isPickup = carriable != null || target.CompareTag("Pickup");
        bool isStation = station != null;
        bool isConstruct = construct != null;
        bool isInteractable = interactable != null;

        if (!isPickup && !isStation && !isConstruct && !isInteractable)
            return null;

        if (IsCarrying)
        {
            if (isPickup)
                return null;

            if (isStation)
            {
                if (CanPlaceCurrentItemToStation(station))
                    return target;

                return null;
            }

            if (isConstruct)
            {
                if (CanBuildCurrentItemToConstruct(construct))
                    return target;

                return null;
            }

            return null;
        }

        if (Time.time - lastDropTime < dropCooldown && isPickup)
            return null;

        if (isConstruct)
            return null;

        return target;
    }

    private bool CanPlaceCurrentItemToStation(WorkStation_SP station)
    {
        if (station == null) return false;
        if (currentObj == null) return false;

        CarriableObject_SP carriable = currentObj.GetComponent<CarriableObject_SP>();
        if (carriable == null) return false;

        return station.GetRecipeIndexForMaterial(carriable.Material) != -1;
    }

    private bool CanBuildCurrentItemToConstruct(ConstructObject_SP construct)
    {
        if (construct == null) return false;
        if (currentObj == null) return false;

        CarriableObject_SP carriable = currentObj.GetComponent<CarriableObject_SP>();
        if (carriable == null) return false;

        return construct.CanBuildWith(carriable);
    }

    private IEnumerator HoldWorkRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.05f);

        while (interactHeld)
        {
            if (!IsCarrying && TryGetCurrentTargetStation(out WorkStation_SP station))
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
            if (TryGetCurrentTargetStation(out WorkStation_SP station))
            {
                PlaceToStation(station);
                return;
            }

            if (currentTarget != null)
            {
                InteractWithTarget(currentTarget);
                return;
            }

            Drop();
            return;
        }

        if (currentTarget != null)
            InteractWithTarget(currentTarget);
    }

    public IPickupable GetCurrentItem()
    {
        return currentCarriable;
    }

    private bool TryGetCurrentTargetStation(out WorkStation_SP station)
    {
        station = null;

        if (currentTarget == null)
            return false;

        station = currentTarget.GetComponent<WorkStation_SP>();

        if (station == null)
            station = currentTarget.GetComponentInChildren<WorkStation_SP>(true);

        return station != null;
    }

    private void SetCurrentTarget(GameObject newTarget)
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

    private void ApplyHighlight(GameObject target)
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

        if (currentCarriable != null)
            currentCarriable.OnDrop();

        if (carriedRb != null)
            carriedRb.isKinematic = false;

        SetHeldItemCollision(false);

        ClearCurrentTarget();
        ClearLocalCarryReferences();

        lastDropTime = Time.time;

        Debug.Log("Item býrakýldý");
    }

    private void Pickup(GameObject obj)
    {
        if (obj == null) return;

        IPickupable item = obj.GetComponent<IPickupable>();
        if (item == null) return;

        Rigidbody itemRb = item.GetRigidbody();
        if (itemRb == null) return;

        ClearCurrentTarget();

        currentCarriable = item;
        currentObj = obj;
        carriedRb = itemRb;
        carriedCollider = carriedRb.GetComponent<Collider>();

        carriedRb.transform.SetParent(null);

        currentCarriable.OnPickUp();
        carriedRb.isKinematic = true;

        if (holdPoint != null)
        {
            carriedRb.transform.position = holdPoint.position;
            carriedRb.transform.rotation = holdPoint.rotation;
        }

        SetHeldItemCollision(true);

        Debug.Log("Item alýndý: " + obj.name);
    }

    private void PlaceToStation(WorkStation_SP station)
    {
        if (station == null) return;
        if (currentObj == null) return;

        CarriableObject_SP carriable = currentObj.GetComponent<CarriableObject_SP>();
        if (carriable == null) return;

        int recipeIndex = station.GetRecipeIndexForMaterial(carriable.Material);

        if (recipeIndex == -1)
        {
            Debug.Log("Bu materyal burada kullanýlamaz");
            return;
        }

        GameObject placedObj = currentObj;

        SetHeldItemCollision(false);

        ClearCurrentTarget();
        ClearLocalCarryReferences();

        station.PlaceItem(carriable, recipeIndex);

        Debug.Log("Item WorkStation'a yerleþtirildi: " + placedObj.name);
    }

    private void InteractWithTarget(GameObject target)
    {
        if (target == null) return;

        CarriableObject_SP pickup = target.GetComponent<CarriableObject_SP>();

        if (pickup != null || target.CompareTag("Pickup"))
        {
            if (IsCarrying) return;

            Pickup(target);
            return;
        }

        ConstructObject_SP construct = target.GetComponentInParent<ConstructObject_SP>();

        if (construct != null)
        {
            BuildConstruct(construct);
            return;
        }

        IInteractable interactable = target.GetComponent<IInteractable>();
        interactable?.Interact();
    }

    private void BuildConstruct(ConstructObject_SP construct)
    {
        if (construct == null) return;
        if (!IsCarrying) return;
        if (currentObj == null) return;

        CarriableObject_SP carriedObj = currentObj.GetComponent<CarriableObject_SP>();
        if (carriedObj == null) return;

        bool built = construct.TryBuild(carriedObj);
        if (!built) return;

        GameObject destroyObj = currentObj;

        SetHeldItemCollision(false);

        ClearCurrentTarget();
        ClearLocalCarryReferences();

        if (destroyObj != null)
            Destroy(destroyObj);

        Debug.Log("Build tamamlandý");
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
        currentObj = null;
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