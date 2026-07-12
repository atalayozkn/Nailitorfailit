using GameData;
using Interactions;
using UnityEngine;
using UnityEngine.Events;

namespace ItemScript
{
    public class ConstructObject_SP : MonoBehaviour, IInteractable
    {
        [Header("Component References")]
        [SerializeField] private GameObject bluePrintObject;
        [SerializeField] private InteractableType interactableType;
        [SerializeField] private Collider interactionCollider;

        [Header("GameObject References")]
        [SerializeField] private GameObject brickObject;
        [SerializeField] private GameObject glassObject;
        [SerializeField] private GameObject stoneObject;
        [SerializeField] private GameObject woodObject;

        [Header("Events")]
        [SerializeField] private UnityEvent onHoverOnEvent;
        [SerializeField] private UnityEvent onHoverOffEvent;
        [SerializeField] private UnityEvent onInteractEvent;

        public InteractableType InteractableType => interactableType;
        private bool isBuilt = false;
        private PlayerInteract_SP interactionHandler;
        private CarriableType currentCarriableType;
        private void Awake()
        {
            interactionHandler = FindFirstObjectByType<PlayerInteract_SP>();
        }
        public void OnInteract()
        {
            currentCarriableType = interactionHandler.GetCurrentCarriableType();
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
                case CarriableType.Brick:
                    brickObject.SetActive(true);
                    break;
                case CarriableType.Glass:
                    glassObject.SetActive(true);
                    break;
                case CarriableType.Wood:
                    woodObject.SetActive(true);
                    break;
                case CarriableType.Stone:
                    stoneObject.SetActive(true);
                    break;
            }

            bluePrintObject.SetActive(false);
            interactionCollider.enabled = false;

            CarriableObject_SP carriable = interactionHandler.GetCurrentCarriable();
            carriable.OnConsume();
            Invoke(nameof(NotifyInteractionHandler), 0.05f);
        }

        private void NotifyInteractionHandler()
        {
            interactionHandler.ClearCarriedObject();
        }
        public void RequestClosure(GameObject obj)
        {
            obj.SetActive(false);
            interactionCollider.enabled = true;
            bluePrintObject.SetActive(true);
        }
    }
}