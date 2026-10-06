using Interactions;
using UnityEngine;
using UnityEngine.Events;

namespace ItemScript
{
    public class Constructor : MonoBehaviour, IInteractable
    {
        [Header("Component References")]
        [SerializeField] private RoomController roomController;
        [SerializeField] private GameObject bluePrintObject;
        [SerializeField] private InteractableType interactableType;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private CarriableType acceptedType;

        [Header("GameObject References")]
        [SerializeField] private GameObject woodObject;
        [SerializeField] private ObjectHealth woodHealth;
        [SerializeField] private GameObject brickObject;
        [SerializeField] private ObjectHealth brickHealth;
        [SerializeField] private GameObject glassObject;
        [SerializeField] private ObjectHealth glassHealth;

        [Header("Events")]
        [SerializeField] private UnityEvent onHoverOnEvent;
        [SerializeField] private UnityEvent onHoverOffEvent;
        [SerializeField] private UnityEvent onInteractEvent;

        public InteractableType InteractableType => interactableType;
        private PlayerInteractionHandler interactionHandler;
        private CarriableType currentCarriableType;
        private ObjectHealth activeHealthComponent;
        private bool isBuilt = false;
        private void Awake()
        {
            interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
        }
        public void OnInteract()
        {
            var carriable = interactionHandler.GetCurrentCarriable();
            if (carriable == null) return;
            if (carriable.isRawMaterial) return;
            currentCarriableType = interactionHandler.GetCurrentCarriableType();
            if (currentCarriableType != acceptedType) return;
            Construct(currentCarriableType);
            onInteractEvent.Invoke();
        }
        public void OnHoverOn()
        {
            onHoverOnEvent?.Invoke();
        }
        public void OnHoverOff()
        {
            onHoverOffEvent?.Invoke();
        }
        private void Construct(CarriableType type)
        {
            if (isBuilt) return;
            isBuilt = true;

            switch (type)
            {
                case CarriableType.Wood:
                    woodObject.SetActive(true);
                    activeHealthComponent = woodHealth;
                    break;
                case CarriableType.Brick:
                    brickObject.SetActive(true);
                    activeHealthComponent = brickHealth;
                    break;
                case CarriableType.Glass:
                    activeHealthComponent = glassHealth;
                    glassObject.SetActive(true);
                    break;
            }

            bluePrintObject.SetActive(false);
            interactionCollider.enabled = false;

            CarriableObject_SP carriable = interactionHandler.GetCurrentCarriable();
            carriable.OnConsume();
            Invoke(nameof(NotifyInteractionHandler), 0.05f);
        }
        public void ReportCompletion()
        {
            roomController.IncreaseCounter();
        }
        private void NotifyInteractionHandler()
        {
            interactionHandler.ClearCarriedObject();
        }
        public void RequestClosure(GameObject obj)
        {
            roomController.ReduceCounter();
            obj.SetActive(false);
            interactionCollider.enabled = true;
            bluePrintObject.SetActive(true);
            isBuilt = false;
        }
        public bool IsBuilt()
        {
            return isBuilt;
        }
        public float GetCurrentHealthPercent()
        {
            if (activeHealthComponent == null) return 0f;
            return activeHealthComponent.GetCurrentHealthPercent();
        }
    }
}