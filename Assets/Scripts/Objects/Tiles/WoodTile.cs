using Flammables;
using Interactions;
using UnityEngine;
using UnityEngine.Events;
using Wettables;

public class WoodTile : MonoBehaviour, IInteractable, IFlammable, IWettable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private FireHelper fireHelper;
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

    #region FIRE

    public void OnFireStart()
    {
        if (currentPhase == ConstructionPhase.Construction) return;
        fireHelper.StartFire();
    }

    public void OnFireStop()
    {
        if (currentPhase == ConstructionPhase.Construction) return;
        fireHelper.StopFire();
    }

    #endregion

    #region WATER
    public void OnWaterContact()
    {
        if (currentPhase == ConstructionPhase.Construction) return;
        puddleHelper.StartPuddleProcess();
    }
    public void OnElectrocute()
    {
        if (currentPhase == ConstructionPhase.Construction) return;
        puddleHelper.ElectrocutePuddle();
    }
    #endregion
    #region UTILITY

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