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

    #region Water Related
    public void OnWaterContact()
    {
        if (!isFloorTile) return;
        puddleHelper.StartPuddleProcess();
    }
    public void OnElectrocute()
    {
        if (!isFloorTile) return;
        puddleHelper.ElectrocutePuddle();
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
