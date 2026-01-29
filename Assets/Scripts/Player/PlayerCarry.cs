using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Interactions;

namespace PlayerScripts
{
    [RequireComponent(typeof(InteractionDetector))]
    [RequireComponent(typeof(PlayerMove))]
    public class PlayerCarry : NetworkBehaviour
    {
        [Header("Input References")]
        [SerializeField] private InputActionReference m_interactAction;

        [Header("Interaction Settings")]
        [SerializeField] private Transform holdPoint;
        [SerializeField] private float throwHoldThreshold = 0.4f;
        [SerializeField] private float maxChargeTime = 1.5f;

        [Header("Physics Settings")]
       // [SerializeField] private float smoothForce = 15f; // Lowered default since we removed DeltaTime
       // [SerializeField] private float maxDistance = 2.0f;
        [SerializeField] private float minThrowForce = 5f;
        [SerializeField] private float maxThrowForce = 25f;
        [SerializeField] private float speedPerWeight = 0.5f;

        private PlayerMove movementScript;
        private InteractionDetector detector;

        private Rigidbody carriedRb;
        private IPickupable currentCarriable;
        private NetworkObject currentNetObj;

        private float interactPressStartTime;
        public bool IsCarrying => currentCarriable != null;

        private void Awake()
        {
            movementScript = GetComponent<PlayerMove>();
            detector = GetComponent<InteractionDetector>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) m_interactAction?.action.Enable();
        }

        private void Update()
        {
            if (!IsOwner) return;
            HandleInput();
        }


        private void HandleInput()
        {
            if (m_interactAction.action.WasPressedThisFrame())
                interactPressStartTime = Time.time;

            if (m_interactAction.action.WasReleasedThisFrame())
            {
                float pressDuration = Time.time - interactPressStartTime;
                if (IsCarrying && pressDuration >= throwHoldThreshold)
                {
                    float charge = Mathf.Clamp01((pressDuration - throwHoldThreshold) / (maxChargeTime - throwHoldThreshold));
                    ThrowObject(charge);
                }
                else
                {
                    PerformInteraction();
                }
            }
        }

        private void PerformInteraction()
        {
            bool foundTarget = detector.TryFindTarget(transform.position, out IInteractable target, out Collider targetCol);

            if (IsCarrying)
            {
                if (foundTarget)
                {
                    // WorkStation.Interact() true dönerse buraya girer
                    if (target.Interact(currentCarriable))
                    {
                        // SENARYO 1: Ýnþaata tuðla koyduk (Yok olmalý)
                        if (ShouldConsumeHeldItem(targetCol))
                        {
                            ConsumeHeldItem();
                        }
                        // SENARYO 2: WorkStation'a ham madde koyduk (Yok olmamalý ama elimizden çýkmalý)
                        else
                        {
                            // BURASI EKSÝKTÝ: Nesneyi makineye verdik, artýk biz yönetmiyoruz.
                            ReleaseObjectReference();
                        }
                        return;
                    }
                }
                DropObject(); // Hedef yoksa veya etkileþim baþarýsýzsa yere at
            }
            else if (foundTarget) // Not carrying, Found something
            {
                if (target is IPickupable)
                {
                    var netObj = targetCol.GetComponentInParent<NetworkObject>();
                    if (netObj != null) RequestPickup(netObj);
                }
                else
                {
                    target.Interact(null);
                }
            }
        }

        private bool ShouldConsumeHeldItem(Collider target)
        {
            // If the target is a "Construction", only consume if we are NOT using a tool
            if (target.GetComponentInParent<IConstructable>() != null)
            {
                return currentCarriable.Tool == Tools.None;
            }
            return false;
        }


        private void InitializeCarry(IPickupable item, NetworkObject netObj)
        {
            currentCarriable = item;
            currentNetObj = netObj;
            carriedRb = item.GetRigidbody(); // Uses the fix from before

            currentCarriable.OnPickUp();

            // 1. Disable Physics (CRITICAL)
            carriedRb.isKinematic = true;

            // 2. DO NOT USE SetParent. It causes the Netcode error.
            // carriedRb.transform.SetParent(holdPoint); <--- DELETE THIS LINE

            // 3. Teleport it once immediately to look snappy
            carriedRb.transform.position = holdPoint.position;
            carriedRb.transform.rotation = holdPoint.rotation;

            Physics.IgnoreCollision(GetComponent<Collider>(), carriedRb.GetComponent<Collider>(), true);

            UpdateSpeed();
        }

        private void ResetCarryState()
        {
            if (carriedRb != null)
            {
                // 1. Re-enable Physics
                carriedRb.isKinematic = false;

                // 2. Un-parent (make it independent again)
                carriedRb.transform.SetParent(null);

                // 3. Re-enable collisions
                Physics.IgnoreCollision(GetComponent<Collider>(), carriedRb.GetComponent<Collider>(), false);
            }

            carriedRb = null;
            currentCarriable = null;
            currentNetObj = null;
            UpdateSpeed();
        }

        private void MoveHeldObject()
        {
            // Since we are the Owner, we can just snap the transform directly.
            // Because isKinematic = true, physics won't fight us.

            carriedRb.MovePosition(holdPoint.position);
            carriedRb.MoveRotation(holdPoint.rotation);
        }

        private void FixedUpdate()
        {
            if (IsCarrying && carriedRb != null)
            {
                MoveHeldObject();
            }
        }
        

        private void UpdateSpeed()
        {
            if (movementScript == null) return;

            if (IsCarrying)
            {
                // FIX: Less aggressive weight penalty calculation
                // Example: Weight 10 * 0.5 * 0.05 = 0.25 (25% slow)
                float penalty = currentCarriable.Weight * speedPerWeight * 0.05f;
                movementScript.SetSpeedModifier(1f - penalty);
            }
            else
            {
                movementScript.SetSpeedModifier(1f);
            }
        }

        private void DropObject()
        {
            if (currentCarriable != null) currentCarriable.OnDrop();
            ResetCarryState();
        }

        private void ThrowObject(float chargePercent)
        {
            if (!IsCarrying) return;

            Rigidbody rbToThrow = carriedRb;
            // Drop normally to re-enable physics
            if (currentCarriable != null) currentCarriable.OnDrop();
            ResetCarryState();

            float finalForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);
            Vector3 throwDir = (transform.forward + Vector3.up * 0.15f).normalized;
            rbToThrow.AddForce(throwDir * finalForce, ForceMode.Impulse);
        }

        private void ReleaseObjectReference()
        {
            // Çarpýþmalarý tekrar aç (Eðer gerekirse, ama nesne artýk istasyonda olduðu için çok dert deðil)
            if (carriedRb != null)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), carriedRb.GetComponent<Collider>(), false);
            }

            // Referanslarý temizle - böylece FixedUpdate artýk bu objeyi hareket ettirmeye çalýþmaz
            carriedRb = null;
            currentCarriable = null;
            currentNetObj = null;
            UpdateSpeed();
        }
        private void ConsumeHeldItem()
        {
            // FIX: Do NOT call OnDrop(). OnDrop turns physics back on, causing "hanging" or falling.
            // Instead, we just hide it locally and tell server to delete it.

            if (currentNetObj != null)
            {
                // Hide immediately so it doesn't float while waiting for ping
                currentNetObj.gameObject.SetActive(false);
                RequestDespawnServerRpc(currentNetObj);
            }

            // Manually clear state without triggering OnDrop logic
            if (carriedRb != null)
                Physics.IgnoreCollision(GetComponent<Collider>(), carriedRb.GetComponent<Collider>(), false);

            carriedRb = null;
            currentCarriable = null;
            currentNetObj = null;
            UpdateSpeed();
        }

        // --- RPCs ---
        [Rpc(SendTo.Server)]
        private void RequestPickupOnPlayerServerRpc(NetworkObjectReference itemRef)
        {
            if (!itemRef.TryGet(out NetworkObject itemObj)) return;
            if (itemObj.GetComponent<IPickupable>() == null) return;

            itemObj.ChangeOwnership(OwnerClientId);
            ReceiveItemClientRpc(itemRef, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ReceiveItemClientRpc(NetworkObjectReference itemRef, RpcParams rpcParams = default)
        {
            if (itemRef.TryGet(out NetworkObject itemObj))
            {
                var item = itemObj.GetComponent<IPickupable>();
                if (item != null) InitializeCarry(item, itemObj);
            }
        }

        private void RequestPickup(NetworkObject itemNetworkObject)
        {
            if (!IsOwner) return;
            RequestPickupOnPlayerServerRpc(itemNetworkObject);
        }

        [Rpc(SendTo.Server)]
        private void RequestDespawnServerRpc(NetworkObjectReference itemRef)
        {
            if (itemRef.TryGet(out NetworkObject itemNetObj))
            {
                if (itemNetObj.IsSceneObject==true) itemNetObj.Despawn(false);
                else itemNetObj.Despawn(true);
            }
        }
    }
}