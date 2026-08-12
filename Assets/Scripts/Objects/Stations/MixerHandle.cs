using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class MixerHandle : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType = InteractableType.Station;
    [SerializeField] private Mixer connectedMixer;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    public InteractableType InteractableType => interactableType;
    public void OnInteract()
    {
        connectedMixer.CompareFormula();
    }
    public void OnHoverOn()
    {
        onHoverOnEvent?.Invoke();
    }
    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }
}
