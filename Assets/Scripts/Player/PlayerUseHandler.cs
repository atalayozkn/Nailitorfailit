using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerUseHandler : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerInteractionHandler interactionHandler;
    [SerializeField] private InputActionReference useAction;

    private IUsable currentUsable;
    private UseType currentUseType;
    private bool isActive;
    private void OnEnable()
    {
        useAction.action.Enable();
        isActive = true;
    }
    private void OnDisable()
    {
        useAction.action.Disable();
    }
    public void SetActivity(bool condition)
    {
        if (condition == isActive) return;
        isActive = condition;
    }
    private void Update()
    {
        if (!isActive) return;
        if (useAction.action.WasPressedThisFrame())
        {
            HandleUse();
        }
    }
    private void HandleUse()
    {
        CarriableObject_SP currentCarriable = interactionHandler.GetCurrentCarriable();
        if (currentCarriable != null && currentCarriable.TryGetComponent<IUsable>(out IUsable usable))
        {
            currentUsable = usable;
            currentUseType = currentUsable.UseType;
            stateMachine.ChangeToUseState();
        }
        else currentUsable = null;
    }
    public IUsable GetCurrentUsable()
    {
        return currentUsable;
    }
    public UseType GetCurrentUseType()
    {
        Debug.Log(currentUseType);
        return currentUseType;
    }
    public void ClearCurrentUsable()
    {
        currentUsable = null;
    }

}
