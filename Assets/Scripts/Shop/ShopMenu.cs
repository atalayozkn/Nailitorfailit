using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShopMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera shopCamera;
    [SerializeField] private ShopInfo shopInfo;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private ShopMarker marker;
    [SerializeField] private ShopIndex[] shopIndexes;
    [SerializeField] private PlayerStateMachine stateMachine;

    [Header("Input")]
    [SerializeField] private InputActionReference previousIndexAction;
    [SerializeField] private InputActionReference nextIndexAction;
    [SerializeField] private InputActionReference buyAction;
    [SerializeField] private InputActionReference closeAction;

    private int currentIndex;
    private bool isActive;

    private void Awake()
    {
        currentIndex = 0;
        UpdateUI(currentIndex);
        menuRoot.SetActive(false);
        SetActivity(false);
    }
    public void SetActivity(bool condition)
    {
        if (isActive == condition) return;

        isActive = condition;
        menuRoot.SetActive(condition);
        shopCamera.Priority = condition ? 10 : 0;

        if (condition)
        {
            currentIndex = 0;
            UpdateUI(currentIndex);
            EnableInput();
            marker.SetVisual(true);
        }
        else
        {
            DisableInput();
            stateMachine.ChangeToIdleState();
            marker.SetVisual(false);
        }
    }

    private void Update()
    {
        if (!isActive) return;
        ListenForInput();
    }

    private void ListenForInput()
    {
        if (nextIndexAction.action.WasPressedThisFrame()) NextIndex();
        if (previousIndexAction.action.WasPressedThisFrame()) PreviousIndex();
        if (buyAction.action.WasPressedThisFrame()) BuyCurrentItem();
        if (closeAction.action.WasPressedThisFrame()) SetActivity(false);
    }

    private void NextIndex()
    {
        if (shopIndexes.Length == 0) return; 
        currentIndex++;

        if (currentIndex >= shopIndexes.Length) currentIndex = 0;
        UpdateUI(currentIndex);
    }

    private void PreviousIndex()
    {
        if (shopIndexes.Length == 0) return;
        currentIndex--;

        if (currentIndex < 0) currentIndex = shopIndexes.Length - 1;
        UpdateUI(currentIndex);
    }

    private void UpdateUI(int index)
    {
        if (index < 0 || index >= shopIndexes.Length) return;
        marker.MoveTo(shopIndexes[index].transform);
        shopInfo.ActivateIndex(index);
    }

    private void BuyCurrentItem()
    {
        if (currentIndex < 0 || currentIndex >= shopIndexes.Length) return;
        shopIndexes[currentIndex].Buy();
    }

    private void EnableInput()
    {
        previousIndexAction.action.Enable();
        nextIndexAction.action.Enable();
        buyAction.action.Enable();
        closeAction.action.Enable();
    }

    private void DisableInput()
    {
        previousIndexAction.action.Disable();
        nextIndexAction.action.Disable();
        buyAction.action.Disable();
        closeAction.action.Disable();
    }
}