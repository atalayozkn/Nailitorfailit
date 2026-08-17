using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class Shop : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType = InteractableType.Shop;
    [SerializeField] private ShopMenu shopMenu;
    [SerializeField] private PlayerStateMachine stateMachine;

    public InteractableType InteractableType => interactableType;

    [Header("Events")]
    [SerializeField] private UnityEvent onShopOpenedEvent;
    [SerializeField] private UnityEvent onShopClosedEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    private bool isShopOpen;

    #region INTERACTION
    public void OnInteract()
    {
        if (isShopOpen) return;
        stateMachine.movementHandler.SetActivity(false);
        stateMachine.interactionHandler.SetActivity(false);
        Invoke(nameof(ActivateMenu), 0.1f);
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
    private void ActivateMenu()
    {
        stateMachine.ChangeToShopState();
        shopMenu.SetActivity(true);
    }
}