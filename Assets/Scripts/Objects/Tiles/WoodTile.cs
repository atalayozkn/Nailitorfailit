using Flammables;
using Interactions;
using ItemScript;
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
    [SerializeField] private Constructor connectedConstructor;
    [SerializeField] private bool isFloorTile;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;

    private ConstructionPhase currentPhase;
    private PlayerInteractionHandler interactionHandler;

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }
    private void OnEnable()
    {
        currentPhase = ConstructionPhase.Construction;
        gameObject.layer = LayerMask.NameToLayer("Interaction");
    }

    #region INTERACTABLE

    public void OnInteract()
    {
        if (currentPhase == ConstructionPhase.Complete) return;
        if (interactionHandler.IsCarrying()) return;
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
        if (!isFloorTile) return;
        if (currentPhase == ConstructionPhase.Construction) return;
        puddleHelper.StartPuddleProcess();
    }
    public void OnElectrocute()
    {
        if (!isFloorTile) return;
        if (currentPhase == ConstructionPhase.Construction) return;
        puddleHelper.ElectrocutePuddle();
    }
    #endregion

    #region UTILITY
    private void CompleteConstruction()
    {
        gameObject.layer = LayerMask.NameToLayer("Ground");
        currentPhase = ConstructionPhase.Complete;
        connectedConstructor.ReportCompletion();
    }
    public ConstructionPhase GetCurrentPhase()
    {
        return currentPhase;
    }

    #endregion
}