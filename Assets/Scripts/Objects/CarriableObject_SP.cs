using Interactions;
using UnityEngine;
using UnityEngine.Events;

namespace ItemScript
{
    public class CarriableObject_SP :MonoBehaviour, IInteractable, ISpawnable
    {
        [Header("References")]
        [SerializeField] private InteractableType interactableType;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Collider col;
        [SerializeField] private MeshRenderer objectRenderer;
        public bool isRawMaterial = false;
        public CarriableType carriableType;

        [Header("Settings")]
        [SerializeField] private float dropForce = 1f;
        [SerializeField] private float objectDiscardDelay = 3.0f;

        [Header("Events")]
        [SerializeField] private UnityEvent onHoverOnEvent;
        [SerializeField] private UnityEvent onHoverOffEvent;
        [SerializeField] private UnityEvent onInteractEvent;
        [SerializeField] private UnityEvent onConsumeEvent;
        public InteractableType InteractableType => interactableType;

        private ObjectSpawner spawnerObject;
        private PlayerInteractionHandler interactionHandler;
        private bool isOccupied = false;
        private bool isConsumed;
        private Transform attachTransform;
        private void Awake()
        {
            interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
        }

        #region INTERACTABLE

        public void OnInteract()
        {
            if (isConsumed)
            {
                return;
            }

            SnapToCarryTransform();
            onInteractEvent?.Invoke();
        }
        public void OnHoverOn()
        {
            onHoverOnEvent?.Invoke();
        }
        public void OnHoverOff()
        {
            onHoverOffEvent?.Invoke();
        }

        #endregion

        #region SPAWNABLE

        public void OnSpawn(GameObject spawner)
        {
            spawnerObject = spawner.GetComponent<ObjectSpawner>();
        }

        #endregion

        #region PLAYER INTERACTION
        private void SnapToCarryTransform()
        {
            col.enabled = false;
            rb.isKinematic = true;
            rb.Sleep();

            Transform carryTransform = interactionHandler.GetCarryTransform();

            transform.SetParent(carryTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            interactionHandler.RegisterCarriedObject(this);
        }
        public void SnapToRightHand()
        {
            col.enabled = false;
            rb.isKinematic = true;
            rb.Sleep();

            Transform rightHandTransform = interactionHandler.GetRightHandTransform();

            transform.SetParent(rightHandTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            interactionHandler.RegisterCarriedObject(this);
        }
        public void OnDrop(bool shouldThrow = false)
        {
            if (isConsumed) return;
            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();

            if (shouldThrow)
            {
                float angle = Random.Range(0f, 30f);
                float rotation = Random.Range(0f, 360f);
                Quaternion spread = Quaternion.Euler(angle, rotation, 0f);
                Vector3 direction = spread * Vector3.up;
                rb.AddForce(direction * dropForce, ForceMode.Impulse);
            }

            interactionHandler.ClearCarriedObject();
        }
        public void OnThrow(float force)
        {
            if (isConsumed) return;
            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();

            Vector3 direction = interactionHandler.transform.forward;
            rb.AddForce(direction * force, ForceMode.Impulse);
            interactionHandler.ClearCarriedObject();
        }
        public void OnConsume()
        {
            if (isConsumed) return;
            isConsumed = true;

            spawnerObject?.ReduceCounter();
            onConsumeEvent?.Invoke();
            interactionHandler.ClearCarriedObject();

            Destroy(gameObject, objectDiscardDelay);
        }
        #endregion

        #region DOG INTERACTION
        public void PickUpByDog(Transform target)
        {
            //Phyics & Occupation
            isOccupied = true;
            col.enabled = false;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();

            //Clear from player if Player is holding this.
            if (interactionHandler.GetCurrentCarriable() == this)
            {
                interactionHandler.ClearCarriedObject();
            }

            //Attach to new target.
            attachTransform = target;
            transform.SetParent(attachTransform);
            Invoke(nameof(AttachToTransform), 0.1f);
        }
        private void AttachToTransform()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        public void DropByDog(Transform target)
        {
            transform.SetParent(target);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.SetParent(null);

            rb.detectCollisions = true;
            rb.useGravity = true;
            col.enabled = true;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
        public void DestroyedByDog()
        {
            spawnerObject?.ReduceCounter();
            onConsumeEvent?.Invoke();
            isOccupied = false;
            Destroy(gameObject, objectDiscardDelay);
        }
        #endregion

        #region UTILITIES
        public void SetOccupied()
        {
            isOccupied = true;
        }    
        public bool IsOccupied()
        {
            return isOccupied;
        }
        public void SetVisuals(bool condition)
        {
            if (condition == objectRenderer.enabled) return;
            objectRenderer.enabled = condition;
        }
    }

        #endregion
}