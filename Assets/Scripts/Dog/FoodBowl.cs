using Interactions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FoodBowl : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private Slider slider;

    [Header("Settings")]
    [SerializeField] private int maxFood;
    [SerializeField] private int perFillAmount;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent inSufficientFoodEvent;

    public InteractableType InteractableType => interactableType;
    private PlayerInteractionHandler interactionHandler;
    private int currentFood;
    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
        currentFood = maxFood;
        slider.maxValue = maxFood;
        UpdateUI();
    }
    public bool OnInteract()
    {
        var carriableType = interactionHandler.GetCurrentCarriableType();
        if (carriableType != CarriableType.PetFood) return WarningManager.Reject("The bowl needs pet food");

        var currentCarriable = interactionHandler.GetCurrentCarriable();
        if (currentCarriable == null) return false;
        if (currentFood >= maxFood) return WarningManager.Reject("The food bowl is already full");

        currentCarriable.OnConsume();

        FillBowl(perFillAmount);
        return true;
    }
    public void OnHoverOn()
    {
        onHoverOnEvent?.Invoke();
    }
    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }
    private void FillBowl(int amount)
    {
        currentFood += amount;
        if (currentFood > maxFood) currentFood = maxFood;
        UpdateUI();
    }
    public bool HasEnoughFood(int amount)
    {
        if (currentFood - amount > 0f) return true;
        inSufficientFoodEvent?.Invoke();
        return false;
    }
    public void ConsumeFood(int amount)
    {
        currentFood -= amount;
        UpdateUI();
    }
    private void UpdateUI()
    {
        slider.value = currentFood;
    }
}
