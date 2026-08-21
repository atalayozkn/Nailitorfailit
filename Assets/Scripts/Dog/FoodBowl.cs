using Interactions;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class FoodBowl : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private int maxFood;
    [SerializeField] private int perFillAmount;
    [SerializeField] private float decayInterval;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;
    private PlayerInteractionHandler interactionHandler;
    private int currentFood;

    private Coroutine foodDecayRoutine; 


    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();

        currentFood = maxFood;

        if (foodDecayRoutine != null)
        {
            StopCoroutine(foodDecayRoutine);
            foodDecayRoutine = null;
        }

        foodDecayRoutine = StartCoroutine(FoodDecayTick());
    }
    private void OnDisable()
    {
        if (foodDecayRoutine == null) return;
        StopCoroutine(foodDecayRoutine);
        foodDecayRoutine= null;
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
    }
    public bool HasEnoughFood(int amount)
    {
        if (currentFood - amount > 0f) return true;
        return false;
    }
    public void ConsumeFood(int amount)
    {
        currentFood -= amount;
    }
    private IEnumerator FoodDecayTick()
    {
        while (true)
        {
            currentFood--;
            yield return new WaitForSeconds(decayInterval);
        }
    }
}
