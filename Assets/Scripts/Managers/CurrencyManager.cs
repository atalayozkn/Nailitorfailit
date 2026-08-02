using UnityEngine;
using UnityEngine.SceneManagement;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

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
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currencyText = FindFirstObjectByType<CurrencyText>();
        UpdateUI();
    }

    public void GainCurrency(int amount)
    {
        currentValue += amount;
        UpdateUI();
        currencyText?.Gain();
    }

    public void SpendCurrency(int amount)
    {
        currentValue -= amount;
        UpdateUI();
        currencyText?.Spend();
    }

    public void SetCurrency(int amount)
    {
        currentValue = amount;
        UpdateUI();
    }

    public bool HasEnoughCurrency(int amount)
    {
        if (currentValue >= amount) return true;

        currencyText?.Reject();
        return false;
    }

    private void UpdateUI()
    {
        if (currencyText == null) return;
        currencyText.UpdateUI(currentValue);
    }
}