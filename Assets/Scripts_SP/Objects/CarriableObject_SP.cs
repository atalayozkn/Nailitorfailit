using PlayerScripts;
using Mirror;
using UnityEngine;
using Interactions;
using UnityEngine.Events;
public enum CarriableType
{
    Brick,
    Wood,
    Stone,
    Oil,
    Glass
}

namespace ItemScript
{
    public class CarriableObject_SP : MonoBehaviour, IInteractable, ISpawnable
    {
        [Header("References")]
        [SerializeField] private InteractableType interactableType;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Collider col;
        [SerializeField] public CarriableType carriableType;

        [Header("Settings")]
        [SerializeField] private float objectDiscardDelay = 3.0f;

        [Header("Events")]
        [SerializeField] private UnityEvent onHoverOnEvent;
        [SerializeField] private UnityEvent onHoverOffEvent;
        [SerializeField] private UnityEvent onInteractEvent;
        [SerializeField] private UnityEvent onConsumeEvent;
        public InteractableType InteractableType => interactableType;
        private ObjectSpawner spawnerObject;
        private PlayerInteract_SP playerInteraction;

        private void Awake()
        {
            playerInteraction = FindFirstObjectByType<PlayerInteract_SP>();
        }
        //Interactable Related
        #region Interactable
        public void OnInteract()
        {
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

        //Spawnable Related
        #region Spawnable
        public void OnSpawn(GameObject spawner)
        {
            spawnerObject = spawner.gameObject.GetComponent<ObjectSpawner>();
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
        public void OnDrop()
        {
            rb.WakeUp();
            rb.isKinematic = false;
            transform.SetParent(null);
            col.enabled = true;
            playerInteraction.ClearCarriedObject();
        }
        public void OnConsume()
        {
            spawnerObject?.ReduceCounter();
            onConsumeEvent?.Invoke();
            Destroy(gameObject, objectDiscardDelay);
        }
    }
}