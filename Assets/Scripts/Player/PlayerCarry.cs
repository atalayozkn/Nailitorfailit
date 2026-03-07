using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using Interactions;
using ItemScript;

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
        private NetworkIdentity currentNetObj;

        private float interactPressStartTime;
        public bool IsCarrying => currentCarriable != null;

        private void Awake()
        {
            movementScript = GetComponent<PlayerMove>();
            detector = GetComponent<InteractionDetector>();
        }

        public override void OnStartLocalPlayer()
        {
            if (m_interactAction != null) m_interactAction.action.Enable();
        }
        public override void OnStopLocalPlayer()
        {
            if (m_interactAction != null) m_interactAction.action.Disable();
        }

        private void Update()
        {
            if (!isOwned) return;

            HandleInput();
            if (IsCarrying == false && m_interactAction.action.IsPressed())
            {
                TryHoldWork();
            }
        }

        private void TryHoldWork()
        {
            bool foundTarget = detector.TryFindTarget(transform.position, out IInteractable target, out Collider col);

            if (!foundTarget) return;

            if (target is WorkStation station)
            {
                station.RequestHoldWork();
            }
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
                    // WorkStation.Interact() true d�nerse buraya girer
                    if (target.Interact(currentCarriable))
                    {
                        // SENARYO 1: �n�aata tu�la koyduk (Yok olmal�)
                        if (ShouldConsumeHeldItem(targetCol))
                        {
                            ConsumeHeldItem();
                        }
                        // SENARYO 2: WorkStation'a ham madde koyduk (Yok olmamal� ama elimizden ��kmal�)
                        else
                        {
                            // BURASI EKS�KT�: Nesneyi makineye verdik, art�k biz y�netmiyoruz.
                            ReleaseObjectReference();
                        }
                        return;
                    }
                }
                DropObject(); // Hedef yoksa veya etkile�im ba�ar�s�zsa yere at
            }
            else if (foundTarget) // Not carrying, Found something
            {
                if (target is IPickupable)
                {
                    var netObj = targetCol.GetComponentInParent<NetworkIdentity>();
                    //if (netObj != null) RequestPickup(netObj);
                    if (netObj != null) CmdRequestPickup(netObj);
                }
                else
                {
                    target.Interact(null);
                }
            }
        }

        private bool ShouldConsumeHeldItem(Collider target)
        {
            // Sadece Construction gibi özel objelerde tüket
            if (target.GetComponent<ConstructObject>() != null)
            {
                return true;
            }

            return false;
        }


        private void InitializeCarry(IPickupable item, NetworkIdentity netObj)
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
            // �arp��malar� tekrar a� (E�er gerekirse, ama nesne art�k istasyonda oldu�u i�in �ok dert de�il)
            if (carriedRb != null)
            {
                carriedRb.isKinematic = true;
                Physics.IgnoreCollision(GetComponent<Collider>(), carriedRb.GetComponent<Collider>(), false);
            }

            // Referanslar� temizle - b�ylece FixedUpdate art�k bu objeyi hareket ettirmeye �al��maz
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
                CmdRequestDespawn(currentNetObj);
            }

            // Manually clear state without triggering OnDrop logic
            if (carriedRb != null)
                Physics.IgnoreCollision(GetComponent<Collider>(), carriedRb.GetComponent<Collider>(), false);

            carriedRb = null;
            currentCarriable = null;
            currentNetObj = null;
            UpdateSpeed();
        }


        /*[Command]
        private void RequestPickupOnPlayer(NetworkObjectReference itemRef)
        {
            if (!itemRef.TryGet(out NetworkIdentity itemObj)) return;
            if (itemObj.GetComponent<IPickupable>() == null) return;

            itemObj.ChangeOwnership(OwnerClientId);
            ReceiveItemClientRpc(itemRef, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }
        //IT MAYBE CHANGE **********************************
        [Command]//[Rpc(SendTo.SpecifiedInParams)]
        private void CmdReceiveItem(NetworkObjectReference itemRef, RpcParams rpcParams = default)
        {
            if (itemRef.TryGet(out NetworkIdentity itemObj))
            {
                var item = itemObj.GetComponent<IPickupable>();
                if (item != null) InitializeCarry(item, itemObj);
            }
        }

        private void RequestPickup(NetworkIdentity itemNetworkObject)
        {
            if (!IsOwner) return;
            RequestPickupOnPlayerServerRpc(itemNetworkObject);
        }*/

        [Command]
        private void CmdRequestDespawn(NetworkIdentity itemNetObj)
        {
            if (itemNetObj != null)
            {
                NetworkServer.Destroy(itemNetObj.gameObject);
            }
        }


        [Command]
        private void CmdRequestPickup(NetworkIdentity itemObj)
        {
            if (itemObj == null) return;
            if (itemObj.GetComponent<IPickupable>() == null) return;

            // Mirror'da yetki verme (Opsiyonel, eğer istemci fizik kontrolü yapacaksa)
            itemObj.RemoveClientAuthority();
            itemObj.AssignClientAuthority(connectionToClient);

            // Sadece bu oyuncuya "Eline al" mesajı gönder
            TargetReceiveItem(connectionToClient, itemObj);
        }
        [TargetRpc] // Sadece belli bir kişiye giden RPC
        private void TargetReceiveItem(NetworkConnection target, NetworkIdentity itemObj)
        {
            if (itemObj != null)
            {
                var item = itemObj.GetComponent<IPickupable>();
                if (item != null) InitializeCarry(item, itemObj);
            }
        }
    }
}