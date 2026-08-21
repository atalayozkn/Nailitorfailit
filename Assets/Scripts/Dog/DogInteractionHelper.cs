using Interactions;
using UnityEngine;

public class DogInteractionHelper : MonoBehaviour,IInteractable
{
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private DogStateMachine stateMachine;
    public InteractableType InteractableType => interactableType;

    private bool isInteracted = false;
    public void OnInteract()
    {
        if (isInteracted) return;
        isInteracted = true;
        stateMachine.ChangeToAffectionState();
    }
    public void OnHoverOn()
    {

    }
    public void OnHoverOff()
    {

    }

}
