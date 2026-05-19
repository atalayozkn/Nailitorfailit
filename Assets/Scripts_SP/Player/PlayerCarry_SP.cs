using UnityEngine;
using UnityEngine.InputSystem;
using Interactions;
using ItemScript;

namespace PlayerScripts
{
    public class PlayerCarry_SP : MonoBehaviour
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

        private PlayerMove_SP movementScript;
        private InteractionDetector detector;

        private Rigidbody carriedRb;
        private IPickupable currentCarriable;
        private GameObject currentObj;

        private float interactPressStartTime;

        public bool IsCarrying => currentCarriable != null;

        private void Awake()
        {
            movementScript = GetComponent<PlayerMove_SP>();
            detector = GetComponent<InteractionDetector>();
        }

        private void OnEnable()
        {
            if (m_interactAction != null)
                m_interactAction.action.Enable();
        }

        private void OnDisable()
        {
            if (m_interactAction != null)
                m_interactAction.action.Disable();
        }

        private void Update()
        {
            if (m_interactAction == null || m_interactAction.action == null)
                return;

            HandleInput();

            if (!IsCarrying && m_interactAction.action.IsPressed())
            {
                TryHoldWork();
            }
        }

        private void FixedUpdate()
        {
            if (IsCarrying && carriedRb != null)
            {
                MoveHeldObject();
            }
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

            if (target is WorkStation_SP station)
            {
                station.RequestHoldWork();
            }
        }

        private void HandleInput()
        {
            if (m_interactAction.action.WasPressedThisFrame())
            {
                interactPressStartTime = Time.time;
            }

            if (m_interactAction.action.WasReleasedThisFrame())
            {
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

        private bool ShouldConsumeHeldItem(Collider target)
        {
            if (target.GetComponent<ConstructObject_SP>() != null)
            {
                return true;
            }

            return false;
        }

        private void InitializeCarry(IPickupable item, GameObject obj)
        {
            currentCarriable = item;
            currentObj = obj;
            carriedRb = item.GetRigidbody();

            if (carriedRb == null) return;

            currentCarriable.OnPickUp();

            carriedRb.isKinematic = true;

            carriedRb.transform.position = holdPoint.position;
            carriedRb.transform.rotation = holdPoint.rotation;

            Collider playerCollider = GetComponent<Collider>();
            Collider itemCollider = carriedRb.GetComponent<Collider>();

            if (playerCollider != null && itemCollider != null)
            {
                Physics.IgnoreCollision(playerCollider, itemCollider, true);
            }

            UpdateSpeed();
        }

        private void ResetCarryState()
        {
            if (carriedRb != null)
            {
                carriedRb.isKinematic = false;
                carriedRb.transform.SetParent(null);

                Collider playerCollider = GetComponent<Collider>();
                Collider itemCollider = carriedRb.GetComponent<Collider>();

                if (playerCollider != null && itemCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, itemCollider, false);
                }
            }

            carriedRb = null;
            currentCarriable = null;
            currentObj = null;

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

                Collider playerCollider = GetComponent<Collider>();
                Collider itemCollider = carriedRb.GetComponent<Collider>();

                if (playerCollider != null && itemCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, itemCollider, false);
                }
            }

            carriedRb = null;
            currentCarriable = null;
            currentObj = null;

            UpdateSpeed();
        }

        private void ConsumeHeldItem()
        {
            if (currentObj != null)
            {
                Destroy(currentObj);
            }

            if (carriedRb != null)
            {
                Collider playerCollider = GetComponent<Collider>();
                Collider itemCollider = carriedRb.GetComponent<Collider>();

                if (playerCollider != null && itemCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, itemCollider, false);
                }
            }

            carriedRb = null;
            currentCarriable = null;
            currentObj = null;

            UpdateSpeed();
        }

        public void RequestPickup(GameObject itemObj)
        {
            if (itemObj == null) return;

            var item = itemObj.GetComponent<IPickupable>();
            if (item == null) return;

            InitializeCarry(item, itemObj);
        }
    }
}