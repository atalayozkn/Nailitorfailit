using System.Collections;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using Interactions;
using ItemScript;

namespace PlayerScripts
{
    public class PlayerCarry : NetworkBehaviour
    {
        [Header("Input References")]
        [SerializeField] private InputActionReference m_interactAction;

        [Header("Interaction Settings")]
        [SerializeField] private Transform holdPoint;
        [SerializeField] private float throwHoldThreshold = 0.4f;
        [SerializeField] private float maxChargeTime = 1.5f;

        [Header("Physics Settings")]
        [SerializeField] private float minThrowForce = 5f;
        [SerializeField] private float maxThrowForce = 25f;
        [SerializeField] private float speedPerWeight = 0.5f;

        private PlayerMove movementScript;
        private InteractionDetector detector;

        private Rigidbody carriedRb;
        private IPickupable currentCarriable;
        private NetworkIdentity currentNetObj;

        private float interactPressStartTime;
        private Coroutine holdWorkRoutine;
        private bool interactHeld;

        public bool IsCarrying => currentCarriable != null;

        private void Awake()
        {
            movementScript = GetComponent<PlayerMove>();
            detector = GetComponent<InteractionDetector>();
        }

        public override void OnStartLocalPlayer()
        {
            if (m_interactAction != null)
                m_interactAction.action.Enable();
        }

        public override void OnStopLocalPlayer()
        {
            if (m_interactAction != null)
                m_interactAction.action.Disable();

            StopHoldWorkRoutine();
        }

        private void Update()
        {
            if (!isOwned) return;
            if (m_interactAction == null || m_interactAction.action == null) return;

            if (m_interactAction.action.WasPressedThisFrame())
            {
                interactPressStartTime = Time.time;
                interactHeld = true;

                if (!IsCarrying)
                    StartHoldWorkRoutine();
            }

            if (m_interactAction.action.WasReleasedThisFrame())
            {
                interactHeld = false;
                StopHoldWorkRoutine();

                float pressDuration = Time.time - interactPressStartTime;

                if (IsCarrying && pressDuration >= throwHoldThreshold)
                {
                    float charge = Mathf.Clamp01(
                        (pressDuration - throwHoldThreshold) /
                        (maxChargeTime - throwHoldThreshold)
                    );

                    ThrowObject(charge);
                }
            }
        }

        private void FixedUpdate()
        {
            if (IsCarrying && carriedRb != null)
            {
                MoveHeldObject();
            }
        }

        private void StartHoldWorkRoutine()
        {
            if (holdWorkRoutine != null) return;

            holdWorkRoutine = StartCoroutine(HoldWorkRoutine());
        }

        private void StopHoldWorkRoutine()
        {
            if (holdWorkRoutine != null)
            {
                StopCoroutine(holdWorkRoutine);
                holdWorkRoutine = null;
            }
        }

        private IEnumerator HoldWorkRoutine()
        {
            while (interactHeld && !IsCarrying)
            {
                TryHoldWork();
                yield return null;
            }

            holdWorkRoutine = null;
        }

        private void TryHoldWork()
        {
            if (detector == null) return;

            bool foundTarget = detector.TryFindTarget(
                transform.position,
                out IInteractable target,
                out Collider col
            );

            if (!foundTarget) return;

            if (target is WorkStation station)
            {
                station.RequestHoldWork();
            }
        }

        private bool ShouldConsumeHeldItem(Collider target)
        {
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
            carriedRb = item.GetRigidbody();

            if (carriedRb == null) return;

            currentCarriable.OnPickUp();

            carriedRb.isKinematic = true;

            carriedRb.transform.position = holdPoint.position;
            carriedRb.transform.rotation = holdPoint.rotation;

            Physics.IgnoreCollision(
                GetComponent<Collider>(),
                carriedRb.GetComponent<Collider>(),
                true
            );

            UpdateSpeed();
        }

        private void ResetCarryState()
        {
            if (carriedRb != null)
            {
                carriedRb.isKinematic = false;
                carriedRb.transform.SetParent(null);

                Physics.IgnoreCollision(
                    GetComponent<Collider>(),
                    carriedRb.GetComponent<Collider>(),
                    false
                );
            }

            carriedRb = null;
            currentCarriable = null;
            currentNetObj = null;

            UpdateSpeed();
        }

        private void MoveHeldObject()
        {
            carriedRb.MovePosition(holdPoint.position);
            carriedRb.MoveRotation(holdPoint.rotation);
        }

        private void UpdateSpeed()
        {
            if (movementScript == null) return;

            if (IsCarrying)
            {
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
            if (currentCarriable != null)
                currentCarriable.OnDrop();

            ResetCarryState();
        }

        private void ThrowObject(float chargePercent)
        {
            if (!IsCarrying) return;

            Rigidbody rbToThrow = carriedRb;

            if (currentCarriable != null)
                currentCarriable.OnDrop();

            ResetCarryState();

            if (rbToThrow == null) return;

            float finalForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);
            Vector3 throwDir = (transform.forward + Vector3.up * 0.15f).normalized;

            rbToThrow.AddForce(throwDir * finalForce, ForceMode.Impulse);
        }

        private void ReleaseObjectReference()
        {
            if (carriedRb != null)
            {
                carriedRb.isKinematic = true;

                Physics.IgnoreCollision(
                    GetComponent<Collider>(),
                    carriedRb.GetComponent<Collider>(),
                    false
                );
            }

            carriedRb = null;
            currentCarriable = null;
            currentNetObj = null;

            UpdateSpeed();
        }

        private void ConsumeHeldItem()
        {
            if (currentNetObj != null)
            {
                currentNetObj.gameObject.SetActive(false);
                CmdRequestDespawn(currentNetObj);
            }

            if (carriedRb != null)
            {
                Physics.IgnoreCollision(
                    GetComponent<Collider>(),
                    carriedRb.GetComponent<Collider>(),
                    false
                );
            }

            carriedRb = null;
            currentCarriable = null;
            currentNetObj = null;

            UpdateSpeed();
        }

        [Command]
        private void CmdRequestDespawn(NetworkIdentity itemNetObj)
        {
            if (itemNetObj != null)
            {
                NetworkServer.Destroy(itemNetObj.gameObject);
            }
        }

        [Command]
        public void CmdRequestPickup(NetworkIdentity itemObj)
        {
            if (itemObj == null) return;
            if (itemObj.GetComponent<IPickupable>() == null) return;

            itemObj.RemoveClientAuthority();
            itemObj.AssignClientAuthority(connectionToClient);

            TargetReceiveItem(connectionToClient, itemObj);
        }

        [TargetRpc]
        private void TargetReceiveItem(NetworkConnection target, NetworkIdentity itemObj)
        {
            if (itemObj != null)
            {
                var item = itemObj.GetComponent<IPickupable>();
                if (item != null)
                    InitializeCarry(item, itemObj);
            }
        }
    }
}