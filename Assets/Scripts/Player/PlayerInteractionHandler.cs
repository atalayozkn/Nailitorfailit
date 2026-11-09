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

    // Singleplayer carry inventory. The item in the active slot is the one in hand (currentCarriable),
    // the others are stowed: hidden, still parented to the player, and ignored by weight and station checks.
    // Multiplayer syncs a single carried item per player, so there only slot 0 is ever used.
    public const int InventorySize = 3;
    private static readonly string[] SlotActionNames = { "Slot1", "Slot2", "Slot3" };
    private readonly CarriableObject_SP[] slots = new CarriableObject_SP[InventorySize];
    private readonly InputAction[] slotActions = new InputAction[InventorySize];
    private InputAction nextSlotAction;
    private InputAction previousSlotAction;
    private int activeSlot;

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
        // So is the carry inventory bar.
        if (!NetworkModeHelper.IsNetworked)
        {
            LookAtInfoUI.Create(this);
            InventoryUI.Create(this);
        }
    }
    private void OnEnable()
    {
        interactAction.action.Enable();
        EnableSlotActions(true);
        isActive = true;
        detectionRoutine = StartCoroutine(TargetDetectionRoutine());
    }
    private void OnDisable()
    {
        interactAction.action.Disable();
        EnableSlotActions(false);
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
        HandleSlotInput();
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

        // A hover error on one object must not stop the detection coroutine, or targeting freezes for good.
        try { currentInteractable?.OnHoverOff(); }
        catch (System.Exception e) { Debug.LogException(e); }
        currentInteractable = interactable;

        if (currentInteractable == null)
        {
            currentInteractableType = default;
            return;
        }

        currentInteractableType = currentInteractable.InteractableType;
        try { currentInteractable.OnHoverOn(); }
        catch (System.Exception e) { Debug.LogException(e); }
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
            // With a free slot the held item is stowed by RegisterCarriedObject, otherwise it is swapped out.
            if (PickupWouldSwap()) currentCarriable.OnDrop();
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
        PlaceInInventory(carriable);

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
        if (currentCarriable != null) RemoveFromSlots(currentCarriable);
        else slots[activeSlot] = null;

        currentCarriable = null;
        currentCarriableType = default;
        isCarrying = false;
        useHandler.ClearCurrentUsable();
        PlayerStates state = stateMachine.currentPlayerState;
        if (state != PlayerStates.Slipping && state != PlayerStates.Using && state != PlayerStates.Dead && state != PlayerStates.Stunned) stateMachine.ForceUpdateIdle();
        pressureHandler.SetHeavy(false);
    }
    // Drops whatever the player is holding where it currently is (used on death so it doesn't respawn with the player).
    public void DropCarriedObject()
    {
        if (currentCarriable == null) return;
        currentCarriable.OnDrop();
    }
    // Called by a carriable leaving the player (drop, throw, consume). A stowed item only frees its own slot,
    // so the item in hand stays put. Multiplayer keeps the old single-item behaviour.
    public void ReleaseCarried(CarriableObject_SP carriable)
    {
        if (!UsesInventory || carriable == currentCarriable)
        {
            ClearCarriedObject();
            return;
        }

        // Not held and not stowed: nothing of the player's to clear.
        if (RemoveFromSlots(carriable)) carriable.SetStowed(false);
    }
    // Death: every carried item stays at the death spot. Stowed ones get a small toss so they don't stack inside each other.
    public void DropAllCarried()
    {
        for (int i = 0; i < InventorySize; i++)
        {
            CarriableObject_SP stowed = slots[i];
            if (stowed == null || stowed == currentCarriable) continue;
            stowed.OnDrop(true);
            slots[i] = null;
        }
        DropCarriedObject();
    }
    // Stun / car hit: the item in hand flies off, stowed items stay in the inventory.
    public void ThrowHeldObject()
    {
        if (currentCarriable == null) return;
        currentCarriable.OnDrop(true);
    }
    // True when picking something up now would drop the held item instead of stowing it.
    public bool PickupWouldSwap()
    {
        if (currentCarriable == null) return false;
        return !UsesInventory || FirstEmptySlot() < 0;
    }
    public int GetActiveSlot()
    {
        return activeSlot;
    }
    public CarriableObject_SP GetSlotItem(int index)
    {
        if (index < 0 || index >= InventorySize) return null;
        return slots[index];
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

    #region INVENTORY

    private bool UsesInventory => !NetworkModeHelper.IsNetworked;

    // The slot actions live in the same map as Interact, so no extra inspector wiring is needed.
    private void EnableSlotActions(bool condition)
    {
        InputActionMap map = interactAction != null ? interactAction.action.actionMap : null;
        if (map == null) return;

        for (int i = 0; i < InventorySize; i++)
        {
            if (slotActions[i] == null) slotActions[i] = map.FindAction(SlotActionNames[i]);
        }
        if (nextSlotAction == null) nextSlotAction = map.FindAction("NextSlot");
        if (previousSlotAction == null) previousSlotAction = map.FindAction("PreviousSlot");

        foreach (InputAction action in slotActions) SetActionEnabled(action, condition);
        SetActionEnabled(nextSlotAction, condition);
        SetActionEnabled(previousSlotAction, condition);
    }
    private static void SetActionEnabled(InputAction action, bool condition)
    {
        if (action == null) return;
        if (condition) action.Enable();
        else action.Disable();
    }
    private void HandleSlotInput()
    {
        if (!UsesInventory) return;

        for (int i = 0; i < InventorySize; i++)
        {
            if (slotActions[i] != null && slotActions[i].WasPressedThisFrame())
            {
                SelectSlot(i);
                return;
            }
        }
        if (nextSlotAction != null && nextSlotAction.WasPressedThisFrame()) SelectSlot((activeSlot + 1) % InventorySize);
        else if (previousSlotAction != null && previousSlotAction.WasPressedThisFrame()) SelectSlot((activeSlot + InventorySize - 1) % InventorySize);
    }
    // Only from calm states: switching mid-use, mid-interaction or while ragdolled would break their animations.
    private bool CanSwitchSlot()
    {
        PlayerStates state = stateMachine.currentPlayerState;
        return state == PlayerStates.Idle || state == PlayerStates.Navigation || state == PlayerStates.OnAir;
    }
    public void SelectSlot(int index)
    {
        if (!UsesInventory || index < 0 || index >= InventorySize || index == activeSlot) return;
        if (!CanSwitchSlot()) return;

        if (currentCarriable != null) currentCarriable.SetStowed(true);
        activeSlot = index;

        CarriableObject_SP next = slots[index];
        if (next != null)
        {
            next.SetStowed(false);
            RegisterCarriedObject(next);
        }
        else if (currentCarriable != null)
        {
            // The stowed item keeps its slot, so ClearCarriedObject must not see it as the held one.
            currentCarriable = null;
            ClearCarriedObject();
        }
    }
    // Puts a newly carried item in a slot and makes that slot active. The held item, if any, is stowed.
    private void PlaceInInventory(CarriableObject_SP carriable)
    {
        if (!UsesInventory)
        {
            slots[activeSlot] = carriable;
            return;
        }

        int existing = System.Array.IndexOf(slots, carriable);
        if (existing >= 0)
        {
            activeSlot = existing;
            return;
        }

        if (currentCarriable != null && currentCarriable != carriable)
        {
            // Callers that skip HandleInteract still get the swap rule when the inventory is full.
            if (FirstEmptySlot() < 0) currentCarriable.OnDrop();
            else currentCarriable.SetStowed(true);
        }

        int target = slots[activeSlot] == null ? activeSlot : FirstEmptySlot();
        if (target < 0) target = activeSlot;
        slots[target] = carriable;
        activeSlot = target;
    }
    // Destroyed items read as null here, so their slot frees up on its own.
    private int FirstEmptySlot()
    {
        for (int i = 0; i < InventorySize; i++)
        {
            if (slots[i] == null) return i;
        }
        return -1;
    }
    private bool RemoveFromSlots(CarriableObject_SP carriable)
    {
        bool removed = false;
        for (int i = 0; i < InventorySize; i++)
        {
            if (slots[i] != carriable) continue;
            slots[i] = null;
            removed = true;
        }
        return removed;
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
