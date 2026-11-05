using Interactions;
using UnityEngine;
using UnityEngine.Events;

namespace ItemScript
{
    public class Constructor : MonoBehaviour, IInteractable, ILookAtInfo
    {
        [Header("Component References")]
        [SerializeField] private RoomController roomController;
        [SerializeField] private GameObject bluePrintObject;
        [SerializeField] private BlueprintVisual blueprintVisual;
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

        [Header("Tutorial Settings")]
        [SerializeField] private bool isTutorial = false;
        [SerializeField] private TutorialObjective objective;

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
        public bool OnInteract()
        {
            if (isBuilt) return false;
            var carriable = interactionHandler.GetCurrentCarriable();
            if (carriable == null) return false;
            if (carriable.isRawMaterial) return false;
            currentCarriableType = interactionHandler.GetCurrentCarriableType();
            if (currentCarriableType != acceptedType) return false;
            Construct(currentCarriableType);
            onInteractEvent.Invoke();
            return true;
        }
        public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
        {
            if (isBuilt) return default;
            string material = LookAtText.MaterialName(acceptedType);
            string name = $"{material} Tile Spot";
            if (carried == null) return LookAtInfo.Blocked(name, $"Needs a {material} tile");
            if (carried.isRawMaterial) return LookAtInfo.Blocked(name, "Process this material first");
            if (carried.carriableType != acceptedType) return LookAtInfo.Blocked(name, $"Needs a {material} tile");
            return LookAtInfo.Ready(name, "Place tile");
        }
        public void OnHoverOn()
        {
            SetBlueprintHovered(true);
            onHoverOnEvent?.Invoke();
        }
        public void OnHoverOff()
        {
            SetBlueprintHovered(false);
            onHoverOffEvent?.Invoke();
        }
        // Also called by a placed tile, which stays a hologram until it is nailed down.
        public void SetBlueprintHovered(bool condition)
        {
            if (blueprintVisual != null) blueprintVisual.SetHovered(condition);
        }
        private void Construct(CarriableType type)
        {
            if (isBuilt) return;
            isBuilt = true;
            VfxManager.Play(VfxId.TilePlaced, transform.position);

            if (isTutorial) objective?.CompleteCondition();

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
        // Used by RoomMaterialRecipe to decide which material this tile needs.
        public void SetAcceptedType(CarriableType type)
        {
            acceptedType = type;
            if (blueprintVisual != null) blueprintVisual.Refresh();
        }
        public CarriableType GetAcceptedType()
        {
            return acceptedType;
        }
        // The tile this slot builds for the given material, used by the blueprint hologram.
        public GameObject GetTileObject(CarriableType type)
        {
            switch (type)
            {
                case CarriableType.Wood: return woodObject;
                case CarriableType.Brick: return brickObject;
                case CarriableType.Glass: return glassObject;
                default: return null;
            }
        }
        // The tile placed in this slot, or null while it is empty.
        public GameObject GetActiveTile()
        {
            if (woodObject != null && woodObject.activeSelf) return woodObject;
            if (brickObject != null && brickObject.activeSelf) return brickObject;
            if (glassObject != null && glassObject.activeSelf) return glassObject;
            return null;
        }
        public float GetCurrentHealthPercent()
        {
            if (activeHealthComponent == null) return 0f;
            return activeHealthComponent.GetCurrentHealthPercent();
        }
    }
}
