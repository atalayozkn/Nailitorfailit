using Interactions;
using UnityEngine;
using UnityEngine.Events;
using Wettables;

public class BrickTile : MonoBehaviour, IInteractable, IWettable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private ObjectHealth objectHealth;
    [SerializeField] private PuddleHelper puddleHelper;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;
    private ConstructionPhase currentPhase;
    private void OnEnable()
    {
        currentPhase = ConstructionPhase.Construction;
        gameObject.layer = LayerMask.NameToLayer("Interaction");
    }
    #region INTERACTABLE
    public void OnInteract()
    {
        if (currentPhase == ConstructionPhase.Complete) return;

        processHelper.Process();

        if (processHelper.IsCompleted()) CompleteConstruction();
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

    #region Water Related
    public void OnWaterContact()
    {
        puddleHelper.StartPuddleProcess();
    }

    #endregion

    #region UTILITIES
    private void CompleteConstruction()
    {
        gameObject.layer = LayerMask.NameToLayer("Ground");
        currentPhase = ConstructionPhase.Complete;
    }
    public ConstructionPhase GetCurrentPhase()
    {
        return currentPhase;
    }
    #endregion
}
