using System.Collections;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractionHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private Transform carryTransform;
    [SerializeField] private bool DebugMode;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference useAction;

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
    private InteractableType currentInteractableType;

    private CarriableObject_SP currentCarriable;
    private CarriableType currentCarriableType;

    private bool isCarrying;
    private bool isInteractOnCooldown;
    private bool isActive;

    private Coroutine detectionRoutine;

    private readonly RaycastHit[] hits =
        new RaycastHit[12];

    private void Awake()
    {
        if (detectionOrigin == null)
        {
            detectionOrigin = transform;
        }
    }

    private void OnEnable()
    {
        if (interactAction != null &&
            interactAction.action != null)
        {
            interactAction.action.Enable();
        }

        if (useAction != null &&
            useAction.action != null)
        {
            useAction.action.Enable();
        }

        isActive = true;

        detectionRoutine =
            StartCoroutine(TargetDetectionRoutine());
    }

    private void OnDisable()
    {
        if (interactAction != null &&
            interactAction.action != null)
        {
            interactAction.action.Disable();
        }

        if (useAction != null &&
            useAction.action != null)
        {
            useAction.action.Disable();
        }

        if (detectionRoutine != null)
        {
            StopCoroutine(detectionRoutine);
            detectionRoutine = null;
        }

        currentInteractable?.OnHoverOff();
    }

    private void Update()
    {
        if (!isActive) return;
        if (isInteractOnCooldown) return;

        if (useAction != null &&
            useAction.action != null &&
            useAction.action.IsPressed())
        {
            if (HandleUse())
            {
                return;
            }
        }

        if (interactAction != null &&
            interactAction.action != null &&
            interactAction.action.IsPressed())
        {
            HandleInteract();

            isInteractOnCooldown = true;

            Invoke(
                nameof(ResetInteractCooldown),
                interactCooldown
            );
        }
    }

    private IEnumerator TargetDetectionRoutine()
    {
        WaitForSeconds wait =
            new WaitForSeconds(detectionInterval);

        while (true)
        {
            RefreshTarget();
            yield return wait;
        }
    }

    private void RefreshTarget()
    {
        IInteractable interactable =
            FindForwardInteractable();

        if (interactable == null)
        {
            interactable = FindGroundInteractable();
        }

        SetCurrentInteractable(interactable);
    }

    private void SetCurrentInteractable(
        IInteractable interactable)
    {
        if (interactable == currentInteractable)
        {
            return;
        }

        currentInteractable?.OnHoverOff();

        currentInteractable = interactable;

        if (currentInteractable == null)
        {
            currentInteractableType = default;
            return;
        }

        currentInteractableType =
            currentInteractable.InteractableType;

        currentInteractable.OnHoverOn();
    }

    private void HandleInteract()
    {
        if (currentCarriable == null &&
            currentInteractable == null)
        {
            return;
        }

        if (currentCarriable != null &&
            currentInteractable == null)
        {
            currentCarriable.OnDrop();
            return;
        }

        if (currentCarriable != null &&
            currentInteractableType ==
            InteractableType.Grabbable)
        {
            currentCarriable.OnDrop();
            return;
        }

        currentInteractable.OnInteract();
        stateMachine.ChangeToInteractState();
    }

    private bool HandleUse()
    {
        if (currentCarriable == null)
        {
            return false;
        }

        if (!currentCarriable.TryGetComponent<IUsable>(
                out var usable))
        {
            return false;
        }

        stateMachine.ChangeToUseState();
        usable.OnUse();

        return true;
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
            interactableMask
        );

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
            interactableMask
        );

        return GetClosestInteractable(count);
    }

    private IInteractable GetClosestInteractable(
        int hitCount)
    {
        float closestDistance = float.MaxValue;
        IInteractable closestInteractable = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;

            if (hitCollider == null)
            {
                continue;
            }

            if (!hitCollider.TryGetComponent<IInteractable>(
                    out var interactable))
            {
                continue;
            }

            if (hits[i].distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hits[i].distance;
            closestInteractable = interactable;
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

    public bool IsInteracting()
    {
        return interactAction != null && interactAction.action != null && interactAction.action.IsPressed();
    }

    public void RegisterCarriedObject(CarriableObject_SP carriable)
    {
        currentCarriable = carriable;
        currentCarriableType = carriable.carriableType;
        isCarrying = true;
        stateMachine.ForceSwitchToIdleState();
    }

    public void ClearCarriedObject(bool forceIdleState = true)
    {
        currentCarriable = null;
        currentCarriableType = default;
        isCarrying = false;

        if (forceIdleState)
        {
            stateMachine.ForceSwitchToIdleState();
        }
    }

    public void SetActivity(bool condition)
    {
        isActive = condition;
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

        Transform origin =
            detectionOrigin == null
                ? transform
                : detectionOrigin;

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(
            origin.position,
            origin.position +
            origin.forward * forwardDistance
        );

        Vector3 groundOrigin =
            transform.position +
            transform.forward * groundForwardOffset +
            Vector3.up * groundHeight;

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(
            groundOrigin,
            groundOrigin +
            Vector3.down * groundDistance
        );
    }

    #endregion
}