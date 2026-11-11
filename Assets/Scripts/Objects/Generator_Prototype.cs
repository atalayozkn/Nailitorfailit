using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class Generator_Prototype : MonoBehaviour, IInteractable, ILookAtInfo
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
    public float CurrentEnergy => currentEnergy;
    public bool HasPower => currentEnergy > 0f;

    // Fires only when the generator goes from empty to powered or back. Used by GeneratorRunningFX.
    public event System.Action<bool> OnPowerStateChanged;

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
    public bool OnInteract()
    {
        CarriableType type = interactionHandler.GetCurrentCarriableType();
        if (type != CarriableType.Oil) return false;

        CarriableObject_SP currentCarriable = interactionHandler.GetCurrentCarriable();
        if (currentCarriable == null) return false;
        if (currentEnergy >= maxEnergy) return false;

        currentCarriable.OnConsume();
        VfxManager.Play(VfxId.Refuel, transform.position);
        GainEnergy(perOilGainAmount);
        return true;
    }
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        const string name = "Generator";
        if (carried == null || carried.carriableType != CarriableType.Oil) return LookAtInfo.Blocked(name, "Needs oil");
        if (currentEnergy >= maxEnergy) return LookAtInfo.Blocked(name, "Already full");
        return LookAtInfo.Ready(name, "Add oil");
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
    // Same check as HasEnoughEnergy, without its out-of-power effects. Safe to poll.
    public bool HasEnergyFor(float amount)
    {
        return currentEnergy - amount >= 0;
    }
    public bool HasEnoughEnergy(float amount)
    {
        if (currentEnergy - amount >= 0) return true;
        else
        {
            onInsufficientEnergyEvent?.Invoke();
            VfxManager.Play(VfxId.OutOfPower, transform.position);
            return false;
        }
    }
    public void ConsumeEnergy(float amount)
    {
        bool hadPower = HasPower;
        currentEnergy -= amount;
        onConsumeEvent?.Invoke();
        UpdateUI();
        NotifyPowerChange(hadPower);
    }
    public void GainEnergy(float amount)
    {
        bool hadPower = HasPower;
        currentEnergy += amount;
        if (currentEnergy >= maxEnergy) currentEnergy = maxEnergy;
        UpdateUI();
        NotifyPowerChange(hadPower);
    }

    private void NotifyPowerChange(bool hadPower)
    {
        if (hadPower != HasPower) OnPowerStateChanged?.Invoke(HasPower);
    }

    #endregion

    #region Utility

    private void UpdateUI()
    {
        sliderHelper.UpdateSlider(currentEnergy);
    }

    #endregion
}
