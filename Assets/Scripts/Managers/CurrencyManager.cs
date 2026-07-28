using UnityEngine;
using UnityEngine.Events;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Events")]
    [SerializeField] private UnityEvent onGainEvent;
    [SerializeField] private UnityEvent onSpendEvent;
    [SerializeField] private UnityEvent onRejectionEvent;

    private CurrencyText currencyText;
    private int currentValue;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        currencyText = FindFirstObjectByType<CurrencyText>();
    }
    public void GainCurrency(int amount)
    {
        currentValue += amount;
        UpdateUI();
        onGainEvent?.Invoke();
    }
    public void SpendCurrency(int amount)
    {
        currentValue -= amount;
        UpdateUI();
        onSpendEvent?.Invoke();
    }
    public bool HasEnoughCurrency(int amount)
    {
        if (currentValue >= amount) return true;

        onRejectionEvent?.Invoke();
        return false;
    }
    private void UpdateUI()
    {
        if (currencyText == null) return;
        currencyText.UpdateUI(currentValue);
    }
}