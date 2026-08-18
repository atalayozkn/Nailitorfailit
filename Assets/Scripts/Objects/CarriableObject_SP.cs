using Interactions;
using UnityEngine;
using UnityEngine.Events;
public enum CarriableType
{
    Brick,
    Wood,
    Stone,
    Oil,
    Glass,
    EnergyDrink
}

namespace ItemScript
{
    public class CarriableObject_SP :MonoBehaviour, IInteractable, ISpawnable
    {
        [Header("References")]
        [SerializeField] private InteractableType interactableType;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Collider col;

        [SerializeField]
        public CarriableType carriableType;

        [Header("Settings")]
        [SerializeField] private float dropForce = 10f;
        [SerializeField] private float objectDiscardDelay = 3.0f;

        [Header("Events")]
        [SerializeField] private UnityEvent onHoverOnEvent;
        [SerializeField] private UnityEvent onHoverOffEvent;
        [SerializeField] private UnityEvent onInteractEvent;
        [SerializeField] private UnityEvent onConsumeEvent;

        public InteractableType InteractableType => interactableType;

        private ObjectSpawner spawnerObject;
        private PlayerInteractionHandler playerInteraction;

        private bool isConsumed;

        private void Awake()
        {
            playerInteraction = FindFirstObjectByType<PlayerInteractionHandler>();
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

            rb.WakeUp();
            rb.isKinematic = false;
            transform.SetParent(null);
            col.enabled = true;
            playerInteraction.ClearCarriedObject(false);

            if (shouldThrow)
            {
                float angle = Random.Range(0f, 30f);
                float rotation = Random.Range(0f, 360f);
                Quaternion spread = Quaternion.Euler(angle,rotation,0f);
                Vector3 direction = spread * Vector3.up;
                rb.AddForce(direction * dropForce, ForceMode.Impulse);
            }
        }
        public void OnUsed()
        {
            if (isConsumed)
            {
                return;
            }

            playerInteraction.ClearCarriedObject(false);
            OnConsume();
        }

        public void OnConsume()
        {
            if (isConsumed)
            {
                return;
            }

            isConsumed = true;

            spawnerObject?.ReduceCounter();
            onConsumeEvent?.Invoke();

            Destroy(gameObject, objectDiscardDelay);
        }
    }
}