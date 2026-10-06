using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class DogInteractionHelper : MonoBehaviour,IInteractable
{
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private DogStateMachine stateMachine;
    public InteractableType InteractableType => interactableType;

    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;


    private bool isInteracted = false;
    public void OnInteract()
    {
        if (isInteracted) return;
        isInteracted = true;
        Invoke(nameof(ReverseInteractionLock), 15f);
        stateMachine.ChangeToAffectionState();
    }
    public void OnHoverOn()
    {
        onHoverOnEvent?.Invoke();
    }
    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }
    public void ReverseInteractionLock()
    {
        isInteracted = false;
    }

}
