using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class MixerHandle : MonoBehaviour, IInteractable
{
    [SerializeField] private float mixDuration;
    [SerializeField] private InteractableType interactableType = InteractableType.Station;
    [SerializeField] private Mixer connectedMixer;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    public InteractableType InteractableType => interactableType;
    public bool OnInteract()
    {
        return connectedMixer.CompareFormula();
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
