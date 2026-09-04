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
        private PlayerInteractionHandler playerInteraction;
        private bool isOccupied = false;
        private bool isConsumed;
        private Vector3 initialScale;
        private Transform attachTransform;
        private void Awake()
        {
            playerInteraction = FindFirstObjectByType<PlayerInteractionHandler>();
            initialScale = transform.localScale;
        }

        #region INTERACTABLE

        public void OnInteract()
        {
            if (isConsumed)
            {
                return;
            }

            OnPickUp();
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
        private void OnPickUp()
        {
            col.enabled = false;
            rb.isKinematic = true;
            rb.Sleep();

            Transform carryTransform = playerInteraction.GetCarryTransform();

            transform.SetParent(carryTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            playerInteraction.RegisterCarriedObject(this);
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

            playerInteraction.ClearCarriedObject();
        }
        public void OnConsume()
        {
            if (isConsumed) return;
            isConsumed = true;

            spawnerObject?.ReduceCounter();
            onConsumeEvent?.Invoke();
            playerInteraction.ClearCarriedObject();

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
            if (playerInteraction.GetCurrentCarriable() == this)
            {
                playerInteraction.ClearCarriedObject();
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
}