using System.Collections;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract_SP : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform carryTransform;
    [SerializeField] private bool DebugMode;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Detection")]
    [SerializeField] private Transform detectionOrigin;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private float detectionInterval = 0.05f;

    [Header("Forward Detection")]
    [SerializeField] private float forwardDistance = 1.4f;
    [SerializeField] private float forwardRadius = 0.25f;

    [Header("Ground Detection")]
    [SerializeField] private float groundForwardOffset = 0.65f;
    [SerializeField] private float groundHeight = 0.8f;
    [SerializeField] private float groundDistance = 1.3f;
    [SerializeField] private float groundRadius = 0.35f;

    [Header("Interaction")]
    [SerializeField] private float interactCooldown = 1.0f;

    private IInteractable currentInteractable;
    private InteractableType currentType;

    private CarriableObject_SP currentCarriable;
    private CarriableType currentCarriableType;
    private bool isCarrying;
    private bool isInteractOnCooldown = false;

    private Coroutine detectionRoutine;

    private readonly RaycastHit[] hits = new RaycastHit[12];

    private void Awake()
    {
        if (detectionOrigin == null)
            detectionOrigin = transform;
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();

        detectionRoutine = StartCoroutine(TargetDetectionRoutine());
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();

        if (detectionRoutine != null)
            StopCoroutine(detectionRoutine);

        currentInteractable?.OnHoverOff();
    }

    private void Update()
    {
        if (isInteractOnCooldown)
            return;

        if (interactAction.action.IsPressed())
        {
            HandleInteract();

            isInteractOnCooldown = true;
            Invoke(nameof(ResetInteractCooldown), interactCooldown);
        }
    }

    private IEnumerator TargetDetectionRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(detectionInterval);

        while (true)
        {
            RefreshTarget();
            yield return wait;
        }
    }

    private void RefreshTarget()
    {
        IInteractable interactable = FindForwardInteractable();

        if (interactable == null)
            interactable = FindGroundInteractable();

        SetCurrentInteractable(interactable);
    }
    private void SetCurrentInteractable(IInteractable interactable)
    {
        if (interactable == currentInteractable)
            return;

        currentInteractable?.OnHoverOff();

        currentInteractable = interactable;

        if (currentInteractable == null)
        {
            currentType = default;
            return;
        }

        currentType = currentInteractable.InteractableType;
        currentInteractable.OnHoverOn();
    }

    private void HandleInteract()
    {
        if (currentCarriable != null && currentInteractable == null)
        {
            currentCarriable.OnDrop();
            return;
        }

        //Interactable Identified, Action now depends on the type of interactable.
        switch (currentType)
        {
            case InteractableType.Grabbable:
                if (isCarrying) return;
                currentInteractable.OnInteract();
                break;

            case InteractableType.Station:
                currentInteractable.OnInteract();
                break;

            case InteractableType.Constructor:
                currentInteractable.OnInteract();
                break;

            case InteractableType.Shop:
                currentInteractable.OnInteract();
                break;
        }
    }
    #region UTILITIES
    private IInteractable FindForwardInteractable()
    {
        int count = Physics.SphereCastNonAlloc(
            detectionOrigin.position,
            forwardRadius,
            detectionOrigin.forward,
            hits,
            forwardDistance,
            interactableMask);

        return GetClosestInteractable(count);
    }

    private IInteractable FindGroundInteractable()
    {
        Vector3 origin =
            transform.position +
            transform.forward * groundForwardOffset +
            Vector3.up * groundHeight;

        int count = Physics.SphereCastNonAlloc(
            origin,
            groundRadius,
            Vector3.down,
            hits,
            groundDistance,
            interactableMask);

        return GetClosestInteractable(count);
    }

    private IInteractable GetClosestInteractable(int hitCount)
    {
        float closestDistance = float.MaxValue;
        IInteractable closestInteractable = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;

            if (hitCollider == null)
                continue;

            if (!hitCollider.TryGetComponent<IInteractable>(out var interactable))
                continue;

            if (hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                closestInteractable = interactable;
            }
        }

        return closestInteractable;
    }
    public Transform GetCarryTransform()
    {
        return carryTransform;
    }

    public bool IsCarrying()
    {
        return isCarrying;
    }

    public CarriableObject_SP GetCurrentCarriable()
    {
        return currentCarriable;
    }

    public CarriableType GetCurrentCarriableType()
    {
        return currentCarriableType;
    }

    public void RegisterCarriedObject(CarriableObject_SP carriable)
    {
        currentCarriable = carriable;
        currentCarriableType = carriable.carriableType;
        isCarrying = true;
    }

    public void ClearCarriedObject()
    {
        currentCarriable = null;
        currentCarriableType = default;
        isCarrying = false;
    }

    private void ResetInteractCooldown()
    {
        isInteractOnCooldown = false;
    }
    #endregion

    #region DEBUG
    private void OnDrawGizmosSelected()
    {
        if (!DebugMode) return;

        Transform origin = detectionOrigin == null ? transform : detectionOrigin;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            origin.position,
            origin.position + origin.forward * forwardDistance);

        Vector3 groundOrigin =
            transform.position +
            transform.forward * groundForwardOffset +
            Vector3.up * groundHeight;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            groundOrigin,
            groundOrigin + Vector3.down * groundDistance);
    }
    #endregion
}