using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class DogInteractionHelper : MonoBehaviour,IInteractable, ILookAtInfo
{
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private DogStateMachine stateMachine;
    public InteractableType InteractableType => interactableType;

    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;


    private bool isInteracted = false;
    public bool OnInteract()
    {
        if (isInteracted) return false;
        isInteracted = true;
        Invoke(nameof(ReverseInteractionLock), 15f);
        stateMachine.ChangeToAffectionState();
        return true;
    }
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        const string name = "Dog";
        return isInteracted ? LookAtInfo.Blocked(name, "Was just petted") : LookAtInfo.Ready(name, "Pet");
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
