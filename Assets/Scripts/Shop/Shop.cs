using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class Shop : MonoBehaviour, IInteractable
{
    [Header("Interactable")]
    [SerializeField] private InteractableType interactableType = InteractableType.Shop;

    public InteractableType InteractableType => interactableType;

    [Header("References")]
    [SerializeField] private PlayerStateMachine playerStateMachine;
    [SerializeField] private ShopMenu shopMenu;

    [Header("Shop Camera")]
    [SerializeField] private GameObject shopCamera;

    [Header("Events")]
    [SerializeField] private UnityEvent onShopOpenedEvent;
    [SerializeField] private UnityEvent onShopClosedEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    private bool isShopOpen;
    private bool isSubscribedToMenu;

    #region UNITY

    // Shop oluþturulduðunda çalýþýr.
    // ResolveReferences() ile gerekli PlayerStateMachine ve ShopMenu referanslarýný hazýrlar.
    private void Awake()
    {
        ResolveReferences();
    }

    // Shop objesi devre dýþý býrakýldýðýnda çalýþýr.
    // ShopMenu event aboneliðini kaldýrýr ve Shop açýksa Player'ý Shop state'inden çýkarýr.
    private void OnDisable()
    {
        UnsubscribeFromShopMenu();

        if (!isShopOpen) return;

        isShopOpen = false;

        if (playerStateMachine != null)
        {
            playerStateMachine.ExitShopState();
        }
    }

    #endregion

    #region INTERACTION

    // Player Shop ile etkileþime girdiðinde çalýþýr.
    // Shop zaten açýk deðilse OpenShop() fonksiyonunu çaðýrýr.
    public void OnInteract()
    {
        if (isShopOpen) return;

        OpenShop();
    }

    // Player Shop üzerine baktýðýnda veya hover baþladýðýnda çalýþýr.
    // Shop açýk deðilse onHoverOnEvent eventini tetikler.
    public void OnHoverOn()
    {
        if (isShopOpen) return;

        onHoverOnEvent?.Invoke();
    }

    // Player Shop üzerinden bakmayý býraktýðýnda veya hover sona erdiðinde çalýþýr.
    // onHoverOffEvent eventini tetikler.
    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }

    #endregion

    #region SHOP

    // Shop'u açmak için çalýþýr.
    // Referanslarý kontrol eder, PlayerStateMachine'i Shop state'ine geçirir, ShopMenu eventine abone olur ve menüyü açar.
    private void OpenShop()
    {
        if (!ValidateReferences()) return;

        bool enteredShop = playerStateMachine.ChangeToShopState(shopCamera);

        if (!enteredShop) return;

        isShopOpen = true;

        SubscribeToShopMenu();
        shopMenu.OpenMenu();
        onShopOpenedEvent?.Invoke();
    }

    // ShopMenu kapandýðýnda çalýþýr.
    // Event aboneliðini kaldýrýr, Player'ý Shop state'inden çýkarýr ve kapanýþ eventini tetikler.
    private void HandleShopClosed()
    {
        if (!isShopOpen) return;

        isShopOpen = false;

        UnsubscribeFromShopMenu();

        if (playerStateMachine != null)
        {
            playerStateMachine.ExitShopState();
        }

        onShopClosedEvent?.Invoke();
    }

    #endregion

    #region EVENTS

    // ShopMenu.OnShopClosed eventine abone olur.
    // Daha önce abone olunmuþsa tekrar subscription oluþturmaz.
    private void SubscribeToShopMenu()
    {
        if (shopMenu == null) return;
        if (isSubscribedToMenu) return;

        shopMenu.OnShopClosed += HandleShopClosed;
        isSubscribedToMenu = true;
    }

    // ShopMenu.OnShopClosed event aboneliðini kaldýrýr.
    // Subscription yoksa gereksiz iþlem yapmadan çýkar.
    private void UnsubscribeFromShopMenu()
    {
        if (!isSubscribedToMenu) return;

        if (shopMenu != null)
        {
            shopMenu.OnShopClosed -= HandleShopClosed;
        }

        isSubscribedToMenu = false;
    }

    #endregion

    #region REFERENCES

    // Shop'un ihtiyaç duyduðu referanslarý hazýrlar.
    // PlayerStateMachine Inspector'dan atanmadýysa sahneden bulur, ShopMenu boþsa Instance üzerinden alýr.
    private void ResolveReferences()
    {
        if (playerStateMachine == null)
        {
            playerStateMachine = FindFirstObjectByType<PlayerStateMachine>();
        }

        if (shopMenu == null)
        {
            shopMenu = ShopMenu.Instance;
        }
    }

    private bool ValidateReferences()
    {
        if (shopMenu == null)
        {
            shopMenu = ShopMenu.Instance;
        }

        if (playerStateMachine == null)
        {
            Debug.LogWarning($"{name}: PlayerStateMachine atanmadý.");
            return false;
        }

        if (shopMenu == null)
        {
            Debug.LogWarning($"{name}: ShopMenu bulunamadý.");
            return false;
        }

        if (shopCamera == null)
        {
            Debug.LogWarning($"{name}: Shop Camera atanmadý.");
            return false;
        }

        return true;
    }

    #endregion
}