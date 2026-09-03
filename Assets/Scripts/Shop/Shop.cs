using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class Shop : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType = InteractableType.Shop;
    [SerializeField] private Transform spawnTransform;

    public InteractableType InteractableType => interactableType;

    [Header("Settings")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private int cost;

    [Header("Events")]
    [SerializeField] private UnityEvent onPurchaseEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    private PlayerInteractionHandler interactionHandler;

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }

    #region INTERACTION
    public void OnInteract()
    {
        if (!CurrencyManager.Instance.HasEnoughCurrency(cost)) return;
        
        if (interactionHandler != null && !interactionHandler.IsCarrying())
        {
            var obj = Instantiate(objectPrefab,spawnTransform.position,spawnTransform.rotation);
            CurrencyManager.Instance.SpendCurrency(cost);
            onPurchaseEvent?.Invoke();

            if (obj.TryGetComponent<CarriableObject_SP>(out CarriableObject_SP carriable))
            {
                carriable.OnInteract();   
            }
        }
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
}