using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class Generator_Prototype : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private UISliderHelper sliderHelper;

    [Header("Settings")]
    [SerializeField] private float maxEnergy;
    [SerializeField] private float perOilGainAmount;
    [SerializeField] private InteractableType interactableType;

    [Header("Events")]
    [SerializeField] private UnityEvent onConsumeEvent;
    [SerializeField] private UnityEvent onInsufficientEnergyEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;
    private float currentEnergy;
    private PlayerInteractionHandler interactionHandler;
    private void Awake()
    {
        interactionHandler = FindAnyObjectByType<PlayerInteractionHandler>();
        currentEnergy = maxEnergy;
    }
    private void Start()
    {
        sliderHelper.SetMaxValue(maxEnergy);
        UpdateUI();
    }

    #region Interactable
    public void OnInteract()
    {
        CarriableType type = interactionHandler.GetCurrentCarriableType();
        if (type != CarriableType.Oil) return;

        CarriableObject_SP currentCarriable = interactionHandler.GetCurrentCarriable();
        if (currentCarriable == null) return;

        currentCarriable.OnUsed();
        GainEnergy(perOilGainAmount);
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

    #region EnergyRelated
    public bool HasEnoughEnergy(float amount)
    {
        if (currentEnergy - amount >= 0) return true;
        else
        {
            onInsufficientEnergyEvent?.Invoke();
            return false;
        }
    }
    public void ConsumeEnergy(float amount)
    {
        currentEnergy -= amount;
        onConsumeEvent?.Invoke();
        UpdateUI();
    }
    public void GainEnergy(float amount)
    {
        currentEnergy += amount;
        if (currentEnergy >= maxEnergy) currentEnergy = maxEnergy;
        UpdateUI();
    }

    #endregion

    #region Utility

    private void UpdateUI()
    {
        sliderHelper.UpdateSlider(currentEnergy);
    }

    #endregion
}
