using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ShopItemType
{
    Oil,
    Drink
}

[Serializable]
public class ShopItem
{
    [Header("Item Info")]
    public string itemName;
    public ShopItemType itemType;
    public int price;

    [Header("Spawn")]
    public GameObject prefab;
    public Transform spawnPoint;
}

public class ShopMenu : MonoBehaviour
{
    public static ShopMenu Instance { get; private set; }

    public event Action OnShopClosed;

    [Header("Menu Root")]
    [SerializeField] private GameObject menuRoot;

    [Header("Input")]
    [SerializeField] private InputActionReference previousIndexAction;
    [SerializeField] private InputActionReference nextIndexAction;
    [SerializeField] private InputActionReference buyAction;
    [SerializeField] private InputActionReference closeAction;

    [Header("Test Currency Input")]
    [SerializeField] private InputActionReference addCurrencyAction;
    [SerializeField] private int addCurrencyAmount = 1000;

    [Header("Currency")]
    [SerializeField] private int currentCurrency = 100;

    [Header("Shop Items")]
    [SerializeField] private ShopItem[] shopItems;

    [Header("Index Objects")]
    [SerializeField] private GameObject[] indexObjects;

    [Header("Index Images")]
    [SerializeField] private Image[] indexImages;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color notEnoughMoneyColor = Color.red;

    [Header("Texts")]
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text selectedItemText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text statusText;

    [Header("Input Safety")]
    [SerializeField] private float inputStartDelay = 0.15f;

    private int currentIndex;
    private bool isOpen;
    private float inputDelayTimer;

    public bool IsOpen => isOpen;
    public int CurrentCurrency => currentCurrency;
    public int CurrentIndex => currentIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CloseMenuInstant();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        UpdateIndexStates();
        UpdateTexts();

        if (inputDelayTimer > 0f)
        {
            inputDelayTimer -= Time.unscaledDeltaTime;
            return;
        }

        HandleMenuInput();
    }

    public void OpenMenu()
    {
        if (isOpen)
            return;

        isOpen = true;
        currentIndex = 0;
        inputDelayTimer = inputStartDelay;

        if (menuRoot != null)
            menuRoot.SetActive(true);

        EnableMenuInputs();

        ClearStatusText();
        UpdateIndexStates();
        UpdateTexts();
    }

    public void CloseMenu()
    {
        if (!isOpen)
            return;

        isOpen = false;

        DisableMenuInputs();

        if (menuRoot != null)
            menuRoot.SetActive(false);

        ClearStatusText();

        OnShopClosed?.Invoke();
    }

    private void CloseMenuInstant()
    {
        isOpen = false;

        DisableMenuInputs();

        if (menuRoot != null)
            menuRoot.SetActive(false);

        ClearStatusText();
        UpdateIndexStates();
        UpdateTexts();
    }

    private void HandleMenuInput()
    {
        if (previousIndexAction != null &&
            previousIndexAction.action != null &&
            previousIndexAction.action.WasPressedThisFrame())
        {
            PreviousIndex();
            return;
        }

        if (nextIndexAction != null &&
            nextIndexAction.action != null &&
            nextIndexAction.action.WasPressedThisFrame())
        {
            NextIndex();
            return;
        }

        if (buyAction != null &&
            buyAction.action != null &&
            buyAction.action.WasPressedThisFrame())
        {
            BuyCurrentItem();
            return;
        }

        if (closeAction != null &&
            closeAction.action != null &&
            closeAction.action.WasPressedThisFrame())
        {
            CloseMenu();
            return;
        }

        if (addCurrencyAction != null &&
            addCurrencyAction.action != null &&
            addCurrencyAction.action.WasPressedThisFrame())
        {
            AddCurrency(addCurrencyAmount);
            return;
        }


        //Currency Eklemek
        if (addCurrencyAction != null &&
            addCurrencyAction.action != null &&
            addCurrencyAction.action.WasPressedThisFrame())
            {
                AddCurrency(addCurrencyAmount);
                return;
            }
    }

    private void PreviousIndex()
    {
        if (!CanUseMenu())
            return;

        currentIndex--;

        if (currentIndex < 0)
            currentIndex = shopItems.Length - 1;

        ClearStatusText();
        UpdateIndexStates();
        UpdateTexts();
    }

    private void NextIndex()
    {
        if (!CanUseMenu())
            return;

        currentIndex++;

        if (currentIndex >= shopItems.Length)
            currentIndex = 0;

        ClearStatusText();
        UpdateIndexStates();
        UpdateTexts();
    }

    private void BuyCurrentItem()
    {
        if (!CanUseMenu())
            return;

        ShopItem item = shopItems[currentIndex];

        if (item == null)
            return;

        if (item.prefab == null)
        {
            SetStatusText("Prefab eksik!");
            return;
        }

        if (!HasEnoughCurrency(item.price))
        {
            SetStatusText("Para yetmiyor!");
            UpdateIndexStates();
            return;
        }

        SpendCurrency(item.price);
        SpawnPurchasedItem(item);

        SetStatusText(item.itemName + " satýn alýndý.");

        UpdateIndexStates();
        UpdateTexts();
    }

    private void SpawnPurchasedItem(ShopItem item)
    {
        if (item == null)
            return;

        if (item.prefab == null)
            return;

        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = Quaternion.identity;

        if (item.spawnPoint != null)
        {
            spawnPosition = item.spawnPoint.position;
            spawnRotation = item.spawnPoint.rotation;
        }

        Instantiate(item.prefab, spawnPosition, spawnRotation);
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0)
            return;

        currentCurrency += amount;

        SetStatusText("+$" + amount);

        UpdateIndexStates();
        UpdateTexts();
    }

    public bool HasEnoughCurrency(int amount)
    {
        return currentCurrency >= amount;
    }

    private void SpendCurrency(int amount)
    {
        currentCurrency -= amount;

        if (currentCurrency < 0)
            currentCurrency = 0;
    }

    private bool CanUseMenu()
    {
        if (!isOpen)
            return false;

        if (shopItems == null)
            return false;

        if (shopItems.Length == 0)
            return false;

        if (currentIndex < 0 || currentIndex >= shopItems.Length)
            currentIndex = 0;

        return true;
    }

    private void UpdateIndexStates()
    {
        UpdateIndexObjects();
        UpdateIndexColors();
    }

    private void UpdateIndexObjects()
    {
        if (indexObjects == null)
            return;

        for (int i = 0; i < indexObjects.Length; i++)
        {
            if (indexObjects[i] == null)
                continue;

            bool shouldBeActive = isOpen && i == currentIndex;

            indexObjects[i].SetActive(shouldBeActive);
        }
    }

    private void UpdateIndexColors()
    {
        if (indexImages == null)
            return;

        for (int i = 0; i < indexImages.Length; i++)
        {
            if (indexImages[i] == null)
                continue;

            Color targetColor = normalColor;

            if (isOpen && i == currentIndex)
            {
                if (shopItems != null &&
                    i < shopItems.Length &&
                    shopItems[i] != null &&
                    !HasEnoughCurrency(shopItems[i].price))
                {
                    targetColor = notEnoughMoneyColor;
                }
                else
                {
                    targetColor = selectedColor;
                }
            }

            indexImages[i].color = targetColor;
        }
    }

    private void UpdateTexts()
    {
        if (currencyText != null)
            currencyText.text = "$" + currentCurrency;

        if (!CanUseMenu())
        {
            if (selectedItemText != null)
                selectedItemText.text = "";

            if (priceText != null)
                priceText.text = "";

            return;
        }

        ShopItem item = shopItems[currentIndex];

        if (selectedItemText != null)
            selectedItemText.text = item != null ? item.itemName : "";

        if (priceText != null)
            priceText.text = item != null ? "$" + item.price : "";
    }

    private void SetStatusText(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private void ClearStatusText()
    {
        if (statusText != null)
            statusText.text = "";
    }

    private void EnableMenuInputs()
    {
        EnableAction(previousIndexAction);
        EnableAction(nextIndexAction);
        EnableAction(buyAction);
        EnableAction(closeAction);
        EnableAction(addCurrencyAction);

        EnableAction(addCurrencyAction);
    }

    private void DisableMenuInputs()
    {
        DisableAction(previousIndexAction);
        DisableAction(nextIndexAction);
        DisableAction(buyAction);
        DisableAction(closeAction);
        DisableAction(addCurrencyAction);

        DisableAction(addCurrencyAction);
    }

    private void EnableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
            return;

        if (actionReference.action == null)
            return;

        actionReference.action.Enable();
    }

    private void DisableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
            return;

        if (actionReference.action == null)
            return;

        actionReference.action.Disable();
    }
}