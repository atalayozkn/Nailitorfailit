using Interactions;
using ItemScript;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Generator_SP : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private PlayerInteractionHandler interactionHandler;

    [Header("Energy")]
    [SerializeField, Min(1f)] private float maxEnergy = 100f;
    [SerializeField, Range(0f, 100f)] private float startingEnergyPercent = 100f;

    [Header("Fuel")]
    [SerializeField, Min(0f)] private float oilEnergyAmount = 20f;

    [Header("UI")]
    [SerializeField] private Slider energySlider;
    [SerializeField] private TextMeshProUGUI energyText;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onInteractEvent;

    private float currentEnergy;
    private int lastDisplayedPercentage = -1;

    public InteractableType InteractableType => interactableType;
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public bool HasPower => currentEnergy > 0f;

    public event Action<bool> OnPowerStateChanged;

    // Generator oluþturulduðunda çalýþýr.
    // InitializeEnergy() ile baþlangýç enerjisini, InitializeUI() ile UI'ýn baþlangýç durumunu hazýrlar.
    private void Awake()
    {
        InitializeEnergy();
        InitializeUI();
    }

    #region INTERACTION

    // Player Generator ile etkileþime girdiðinde çalýþýr.
    // TryAddCarriedFuel() ile taþýnan yakýtý kontrol eder ve yakýt baþarýyla eklenirse onInteractEvent'i tetikler.
    public void OnInteract()
    {
        if (!TryAddCarriedFuel()) return;

        onInteractEvent?.Invoke();
    }

    // Player Generator üzerine baktýðýnda veya hover baþladýðýnda çalýþýr.
    // onHoverOnEvent eventini tetikler.
    public void OnHoverOn()
    {
        onHoverOnEvent?.Invoke();
    }

    // Player Generator üzerinden bakmayý býraktýðýnda veya hover sona erdiðinde çalýþýr.
    // onHoverOffEvent eventini tetikler.
    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }

    private bool TryAddCarriedFuel()
    {
        if (!ResolveInteractionHandler()) return false;

        CarriableObject_SP carriedObject = interactionHandler.GetCurrentCarriable();

        if (carriedObject == null)
        {
            Debug.Log("Generatorün yakýta ihtiyacý var.");
            return false;
        }

        if (carriedObject.carriableType != CarriableType.Oil)
        {
            Debug.Log("Bu obje Generator yakýtý deðil.");
            return false;
        }

        if (!AddEnergy(oilEnergyAmount)) return false;

        carriedObject.OnConsume();
        return true;
    }

    #endregion

    #region ENERGY

    // Generator'ýn baþlangýç enerjisini ayarlar.
    // startingEnergyPercent deðerini maxEnergy üzerinden hesaplayarak currentEnergy deðerine yazar.
    private void InitializeEnergy()
    {
        float normalizedStart = Mathf.Clamp01(startingEnergyPercent / 100f);
        currentEnergy = maxEnergy * normalizedStart;
    }

    public bool TryConsumeEnergy(float amount)
    {
        if (amount <= 0f || currentEnergy < amount) return false;

        bool previousPowerState = HasPower;
        currentEnergy = Mathf.Max(currentEnergy - amount, 0f);

        HandleEnergyChanged(previousPowerState);
        return true;
    }

    public bool AddEnergy(float amount)
    {
        if (amount <= 0f) return false;

        if (currentEnergy >= maxEnergy)
        {
            Debug.Log("Generator zaten tamamen dolu.");
            return false;
        }

        bool previousPowerState = HasPower;
        currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);

        HandleEnergyChanged(previousPowerState);
        return true;
    }

    public bool HasEnoughEnergy(float amount)
    {
        return amount > 0f && currentEnergy >= amount;
    }

    public float GetEnergyNormalized()
    {
        return maxEnergy > 0f ? Mathf.Clamp01(currentEnergy / maxEnergy) : 0f;
    }

    public float GetEnergyPercent()
    {
        return GetEnergyNormalized() * 100f;
    }

    // Generator'ýn enerjisi deðiþtiðinde çalýþýr.
    // RefreshUI() ile UI'ý günceller ve güç durumu deðiþmiþse OnPowerStateChanged eventini tetikler.
    private void HandleEnergyChanged(bool previousPowerState)
    {
        RefreshUI();

        bool currentPowerState = HasPower;

        if (previousPowerState != currentPowerState)
        {
            OnPowerStateChanged?.Invoke(currentPowerState);
        }
    }

    #endregion

    #region UI

    // Generator UI'ýný baþlangýç için hazýrlar.
    // Slider'ýn minimum ve maksimum deðerlerini ayarlar ve RefreshUI() ile mevcut enerji deðerini ekrana yansýtýr.
    private void InitializeUI()
    {
        if (energySlider != null)
        {
            energySlider.minValue = 0f;
            energySlider.maxValue = 1f;
        }

        RefreshUI();
    }

    // Generator'ýn mevcut enerji deðerini Slider ve yüzde yazýsýna aktarýr.
    // Görünen yüzde deðiþmediyse TextMeshPro yazýsýný tekrar güncellemez.
    private void RefreshUI()
    {
        float normalized = GetEnergyNormalized();

        if (energySlider != null)
        {
            energySlider.SetValueWithoutNotify(normalized);
        }

        int percentage = Mathf.RoundToInt(normalized * 100f);

        if (percentage == lastDisplayedPercentage) return;

        lastDisplayedPercentage = percentage;

        if (energyText != null)
        {
            energyText.SetText("{0}%", percentage);
        }
    }

    #endregion

    #region REFERENCES

    private bool ResolveInteractionHandler()
    {
        if (interactionHandler != null) return true;

        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();

        if (interactionHandler != null) return true;

        Debug.LogWarning($"{name}: PlayerInteractionHandler bulunamadý.");
        return false;
    }

    #endregion
}