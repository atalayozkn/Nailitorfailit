using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class MixerHandle : MonoBehaviour, IInteractable, ILookAtInfo
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
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        return connectedMixer != null ? connectedMixer.GetHandleLookAtInfo() : default;
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
