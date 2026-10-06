using Interactions;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class FoodBowl : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private UIImageFillHelper imageFiller;

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
    }
    public void OnInteract()
    {
        var carriableType = interactionHandler.GetCurrentCarriableType();
        if (carriableType == CarriableType.PetFood)
        {
            var currentCarriable = interactionHandler.GetCurrentCarriable();
            currentCarriable.OnConsume();

            FillBowl(perFillAmount);
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
        float foodPercent = (float)currentFood / (float)maxFood;
        imageFiller.UpdateUI(foodPercent);
    }
}
