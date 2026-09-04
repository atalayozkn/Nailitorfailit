
using System.Collections;
using Interactions;
using Interactions.Networking;
using ItemScript;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerScripts
{
    [RequireComponent(typeof(PlayerInteractionHandler))]
    [RequireComponent(typeof(PlayerStateMachine))]
    public class PlayerInteractionNetwork_MP : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractionHandler interactionHandlerSp;
        [SerializeField] private PlayerStateMachine stateMachine;

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

        private IInteractable currentInteractable;
        private InteractableType currentInteractableType;

        private bool isInteractOnCooldown;
        private Coroutine detectionRoutine;

        private readonly RaycastHit[] hits = new RaycastHit[12];

        private void Awake()
        {
            if (interactionHandlerSp == null) interactionHandlerSp = GetComponent<PlayerInteractionHandler>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            interactAction.action.Enable();
            detectionRoutine = StartCoroutine(TargetDetectionRoutine());
        }

        public override void OnStopLocalPlayer()
        {
            base.OnStopLocalPlayer();

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
            if (!isLocalPlayer) return;
            if (isInteractOnCooldown) return;

            if (interactAction.action.WasPressedThisFrame())
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
            CarriableObject_SP currentCarriable = interactionHandlerSp.GetCurrentCarriable();

            if (currentCarriable == null && currentInteractable == null) return;

            if (currentCarriable != null && currentInteractable == null)
            {
                RequestDrop(currentCarriable);
                return;
            }

            if (currentCarriable != null && currentInteractableType == InteractableType.Grabbable)
            {
                RequestDrop(currentCarriable);
                DispatchInteract(currentInteractable);
                return;
            }

            DispatchInteract(currentInteractable);
            stateMachine.ChangeToInteractState();
        }

        private void DispatchInteract(IInteractable interactable)
        {
            if (interactable is Component component)
            {
                if (component.TryGetComponent(out CarriableObject_MP carriableMp))
                {
                    carriableMp.NotifyPickedUp(interactionHandlerSp);
                    return;
                }

                if (component.TryGetComponent(out IInteractionNetworkProxy proxy))
                {
                    proxy.RequestInteract();
                    return;
                }
            }

            interactable.OnInteract();
        }

        private void RequestDrop(CarriableObject_SP carriable)
        {
            if (carriable.TryGetComponent(out CarriableObject_MP carriableMp))
            {
                carriableMp.NotifyDropped(interactionHandlerSp);
                return;
            }

            carriable.OnDrop();
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

        private void ResetInteractCooldown()
        {
            isInteractOnCooldown = false;
        }

        #endregion
    }
}
