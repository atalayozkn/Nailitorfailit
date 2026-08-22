using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class MailBox : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private float timeRewardAmount;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onTriggerEvent;

    private bool isMailPresent;
    public InteractableType InteractableType => interactableType;

    #region Interactable
    public void OnInteract()
    {
        if (!isMailPresent) return;
        isMailPresent = false;
        GameTimeManager.Instance.IncreaseRoundTime(timeRewardAmount);
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

    public void OnTrigger()
    {
        isMailPresent = true;
        onTriggerEvent?.Invoke();
    }
}
