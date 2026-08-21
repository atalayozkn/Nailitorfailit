using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class MailBox : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onTriggerEvent;

    private bool isMailPresent;
    public InteractableType InteractableType => interactableType;

    #region Interactable
    public void OnInteract()
    {

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
