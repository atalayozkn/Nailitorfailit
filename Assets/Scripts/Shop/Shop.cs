using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class Shop : MonoBehaviour, IInteractable, ILookAtInfo
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
    public bool OnInteract()
    {
        if (!CurrencyManager.Instance.HasEnoughCurrency(cost)) return false;
        if (interactionHandler == null || interactionHandler.IsCarrying()) return false;

        var obj = Instantiate(objectPrefab,spawnTransform.position,spawnTransform.rotation);
        CurrencyManager.Instance.SpendCurrency(cost);
        onPurchaseEvent?.Invoke();

        if (obj.TryGetComponent<CarriableObject_SP>(out CarriableObject_SP carriable))
        {
            carriable.SnapToRightHand();
        }

        return true;
    }
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        string name = LookAtText.ItemName(objectPrefab, "Shop");
        if (CurrencyManager.Instance != null && !CurrencyManager.Instance.CanAfford(cost)) return LookAtInfo.Blocked(name, $"Not enough money ({cost})");
        if (carried != null) return LookAtInfo.Blocked(name, "Your hands are full");
        return LookAtInfo.Ready(name, $"Buy ({cost})");
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
