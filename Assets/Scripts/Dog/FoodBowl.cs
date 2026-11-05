using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FoodBowl : MonoBehaviour, IInteractable, ILookAtInfo
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
        if (carriableType != CarriableType.PetFood) return false;

        var currentCarriable = interactionHandler.GetCurrentCarriable();
        if (currentCarriable == null) return false;
        if (currentFood >= maxFood) return false;

        currentCarriable.OnConsume();

        FillBowl(perFillAmount);
        VfxManager.Play(VfxId.BowlFill, transform.position);
        return true;
    }
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        const string name = "Food Bowl";
        if (carried == null || carried.carriableType != CarriableType.PetFood) return LookAtInfo.Blocked(name, "Needs pet food");
        if (currentFood >= maxFood) return LookAtInfo.Blocked(name, "Already full");
        return LookAtInfo.Ready(name, "Fill");
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
