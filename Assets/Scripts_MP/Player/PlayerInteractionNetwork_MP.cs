
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

        [Tooltip("Hedef isini bir kare kacirsa, bu sure boyunca onceki hedef korunur (saniye).")]
        [SerializeField] private float targetGraceSeconds = 0.35f;

        private IInteractable currentInteractable;
        private InteractableType currentInteractableType;

        private bool isInteractOnCooldown;
        private Coroutine detectionRoutine;

        private readonly RaycastHit[] hits = new RaycastHit[12];

        private void Awake()
        {
            if (interactionHandlerSp == null) interactionHandlerSp = GetComponent<PlayerInteractionHandler>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();

            if (interactionHandlerSp != null)
                interactionHandlerSp.enabled = false;
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            InteractionDebug.Log($"[{name}] local player interaction wrapper [{InteractionDebug.Version}]");

            if (interactAction != null && interactAction.action != null)
                InteractionDebug.Log($"[{name}] interact action '{interactAction.action.name}' " +
                                     $"map='{interactAction.action.actionMap?.name}' " +
                                     $"bindings={interactAction.action.bindings.Count} " +
                                     $"enabled={interactAction.action.enabled}");
            else
                Debug.LogError($"[{name}] interactAction is NOT wired - the interact key can never fire.");

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

            if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
                interactAction.action.Enable();

            if (isInteractOnCooldown) return;

            if (interactAction == null || interactAction.action == null)
            {
                WarnMissingActionOnce();
                return;
            }

            if (!interactAction.action.IsPressed()) return;

            InteractionDebug.Log($"[{name}] interact key down (target={(currentInteractable as Component)?.name ?? "none"}, " +
                                 $"carried={(interactionHandlerSp.GetCurrentCarriable() != null ? interactionHandlerSp.GetCurrentCarriable().name : "none")})");

            if (currentInteractable == null) RefreshTarget();
            if (currentInteractable == null && interactionHandlerSp.GetCurrentCarriable() == null)
            {
                InteractionDebug.Log($"[{name}] interact basildi ama hedef yok (menzilde etkilesim nesnesi yok).");
                return;
            }

            HandleInteract();
            isInteractOnCooldown = true;
            Invoke(nameof(ResetInteractCooldown), Mathf.Clamp(interactCooldown, 0.05f, 0.3f));
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

        private float lastTargetTime;
        private bool missingActionWarned;
        private float nextRangeLogAt;

        private void WarnMissingActionOnce()
        {
            if (missingActionWarned) return;

            missingActionWarned = true;

            string referenceState = interactAction == null ? "missing" : "set";
            Debug.LogError($"[{name}] interactAction/action is null - interact input is dead " +
                           $"(reference {referenceState}).");
        }

        private void LogEmptyDetection()
        {
            if (Time.unscaledTime < nextRangeLogAt) return;

            nextRangeLogAt = Time.unscaledTime + 2f;
            InteractionDebug.Log($"[{name}] no interactable in range " +
                                 $"(forward {forwardDistance}m/r{forwardRadius}, ground {groundDistance}m/r{groundRadius}, " +
                                 $"mask={(int)interactableMask.value})");
        }

        private void RefreshTarget()
        {
            IInteractable interactable = FindForwardInteractable();

            if (interactable == null)
            {
                interactable = FindGroundInteractable();
            }

            if (interactable == null)
            {
                if (currentInteractable != null &&
                    Time.unscaledTime - lastTargetTime <= targetGraceSeconds)
                {
                    return;
                }

                if (currentInteractable == null) LogEmptyDetection();

                SetCurrentInteractable(null);
                return;
            }

            lastTargetTime = Time.unscaledTime;
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
            InteractionDebug.Log($"[{name}] dispatch on '{(interactable as Component)?.name ?? "?"}' " +
                                 $"type={interactable?.InteractableType}");

            if (interactable is Component component)
            {
                if (component.TryGetComponent(out CarriableObject_MP carriableMp))
                {
                    InteractionDebug.Log($"[{name}] -> pickup/drop of networked carriable '{component.name}'");
                    carriableMp.NotifyPickedUp(interactionHandlerSp);
                    return;
                }

                IInteractionNetworkProxy proxy = component.GetComponentInParent<IInteractionNetworkProxy>();

                if (proxy is NetworkBehaviour proxyBehaviour && proxyBehaviour.netIdentity == null)
                {
                    InteractionDebug.Log($"[{name}] proxy {proxy.GetType().Name} on '{component.name}' " +
                                         "has no NetworkIdentity; routing through the player instead");
                    proxy = null;
                }

                if (proxy != null)
                {
                    InteractionDebug.Log($"[{name}] -> proxy {proxy.GetType().Name} for '{component.name}'");

                    proxy.RequestInteract(netIdentity, component);
                    return;
                }

                NetworkIdentity ownIdentity = netIdentity;

                if (ownIdentity != null && TryGetComponent(out PlayerInteractRouter_MP router))
                {
                    InteractionDebug.Log($"[{name}] -> player-routed interact for '{component.name}'");
                    router.RequestInteract(component);
                    return;
                }

                bool clientOnly = NetworkClient.active && !NetworkServer.active;

                if (clientOnly)
                {
                    InteractionDebug.Log($"[{name}] -> NO PROXY for '{component.name}' " +
                                         "- client'ta yerel etkilesim BLOKLANDI (hayalet obje olusmasin)");
                    return;
                }

                InteractionDebug.Log($"[{name}] -> NO PROXY for '{component.name}' (host yerel fallback)");
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
