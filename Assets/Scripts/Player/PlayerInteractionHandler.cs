using System.Collections;
using System.Net;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractionHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private Transform carryTransform;
    [SerializeField] private Transform rightHandTransform;
    [SerializeField] private PlayerPressureHandler pressureHandler;
    [SerializeField] private PlayerUseHandler useHandler;
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

    [Header("Duration Settings")]
    [SerializeField] private float interactCooldown = 1.0f;
    [SerializeField] private float useCooldown = 1.0f;

    // A press on the current target did nothing.
    public event System.Action InteractBlocked;

    private IInteractable currentInteractable;
    private InteractableType currentInteractableType;

    private CarriableObject_SP currentCarriable;
    private CarriableType currentCarriableType;

    private bool isCarrying;
    private bool isInteractOnCooldown;
    private bool hasActedThisPress;
    private bool isActive;

    private Coroutine detectionRoutine;

    private readonly RaycastHit[] hits = new RaycastHit[12];
    private void Start()
    {
        // Multiplayer routes interaction itself, the look-at panel is singleplayer only.
        if (!NetworkModeHelper.IsNetworked) LookAtInfoUI.Create(this);
    }
    private void OnEnable()
    {
        interactAction.action.Enable();
        isActive = true;
        detectionRoutine = StartCoroutine(TargetDetectionRoutine());
    }
    private void OnDisable()
    {
        interactAction.action.Disable();
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
        if (!interactAction.action.IsPressed()) hasActedThisPress = false;
        if (isInteractOnCooldown) return;
        if (interactAction.action.IsPressed())
        {
            WarningManager.IsMuted = hasActedThisPress;
            HandleInteract();
            WarningManager.IsMuted = false;
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
        {
            interactable = FindGroundInteractable();
        }
        SetCurrentInteractable(interactable);
    }
    private void SetCurrentInteractable(IInteractable interactable)
    {
        if (interactable == currentInteractable) return;

        currentInteractable?.OnHoverOff();
        currentInteractable = interactable;

        if (currentInteractable == null)
        {
            currentInteractableType = default;
            return;
        }

        currentInteractableType = currentInteractable.InteractableType;
        currentInteractable.OnHoverOn();
    }
    private void HandleInteract()
    {
        if (currentCarriable == null && currentInteractable == null)
        {
            StopInteractAnimation();
            return;
        }

        if (currentCarriable != null && currentInteractable == null)
        {
            hasActedThisPress = true;
            currentCarriable.OnDrop();
            return;
        }

        if (currentCarriable != null && currentInteractableType == InteractableType.Grabbable)
        {
            hasActedThisPress = true;
            currentCarriable.OnDrop();
            currentInteractable.OnInteract();
            return;
        }

        if (currentCarriable == null && currentInteractableType == InteractableType.Grabbable)
        {
            hasActedThisPress = true;
            currentInteractable.OnInteract();
            return;
        }

        if (currentInteractable.OnInteract())
        {
            hasActedThisPress = true;
            stateMachine.ChangeToInteractState();
        }
        else
        {
            StopInteractAnimation();
            // Holding the input after a success is not a mistake, same rule as WarningManager.IsMuted.
            if (!hasActedThisPress) InteractBlocked?.Invoke();
        }
    }
    // Holding the input keeps the state alive, so a blocked repeat has to end the animation too.
    private void StopInteractAnimation()
    {
        if (stateMachine.currentPlayerState == PlayerStates.Interacting) stateMachine.ChangeToIdleState();
    }
    #region UTILITIES
    private IInteractable FindForwardInteractable()
    {
        int count = Physics.SphereCastNonAlloc(detectionOrigin.position, forwardRadius, detectionOrigin.forward, hits, forwardDistance, interactableMask);
        return GetClosestInteractable(count);
    }

    private IInteractable FindGroundInteractable()
    {
        Vector3 origin = transform.position + transform.forward * groundForwardOffset + Vector3.up * groundHeight;
        int count = Physics.SphereCastNonAlloc(origin, groundRadius, Vector3.down, hits, groundDistance, interactableMask);
        return GetClosestInteractable(count);
    }

    private IInteractable GetClosestInteractable(int hitCount)
    {
        float closestDistance = float.MaxValue;
        IInteractable closestInteractable = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (!hitCollider.TryGetComponent<IInteractable>(out var interactable)) continue;
            if (hits[i].distance >= closestDistance) continue;
            closestDistance = hits[i].distance;
            closestInteractable = interactable;
        }
        return closestInteractable;
    }

    public Transform GetCarryTransform()
    {
        return carryTransform;
    }
    public Transform GetRightHandTransform()
    {
        return rightHandTransform;
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
        return interactAction.action.IsPressed();
    }
    public void RegisterCarriedObject(CarriableObject_SP carriable)
    {
        currentCarriable = carriable;
        currentCarriableType = carriable.carriableType;
        isCarrying = true;
        if (stateMachine.currentPlayerState != PlayerStates.Slipping)
        {
            stateMachine.ForceUpdateIdle();
        }
        pressureHandler.SetHeavy(true);
    }
    public void ClearCarriedObject()
    {
        currentCarriable = null;
        currentCarriableType = default;
        isCarrying = false;
        useHandler.ClearCurrentUsable();
        if (stateMachine.currentPlayerState != PlayerStates.Slipping && stateMachine.currentPlayerState != PlayerStates.Using && stateMachine.currentPlayerState != PlayerStates.Dead) stateMachine.ForceUpdateIdle();
        pressureHandler.SetHeavy(false);
    }
    // Drops whatever the player is holding where it currently is (used on death so it doesn't respawn with the player).
    public void DropCarriedObject()
    {
        if (currentCarriable == null) return;
        currentCarriable.OnDrop();
    }
    // What the look-at panel should show right now. False when there is nothing to show.
    public bool TryGetLookAtInfo(out LookAtInfo info)
    {
        info = default;
        if (!isActive || !enabled || stateMachine.currentPlayerState == PlayerStates.Dead) return false;

        if (currentInteractable is UnityEngine.Object target && target != null)
        {
            if (!(currentInteractable is ILookAtInfo provider)) return false;

            // HandleInteract drops the carried item before grabbing, so grabbables see empty hands.
            CarriableObject_SP carried = currentInteractableType == InteractableType.Grabbable ? null : currentCarriable;
            info = provider.GetLookAtInfo(carried);
            return !string.IsNullOrEmpty(info.Name);
        }

        if (currentCarriable == null) return false;
        info = LookAtInfo.Ready(LookAtText.ItemName(currentCarriable), "Drop");
        return true;
    }
    public string GetInteractBindingLabel(bool gamepad)
    {
        // Without the interaction prefix: the action has a Hold on it, but Update fires on press.
        return interactAction.action.GetBindingDisplayString(InputBinding.MaskByGroup(gamepad ? "Gamepad" : "Keyboard&Mouse"), InputBinding.DisplayStringOptions.DontIncludeInteractions);
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
