using Flammables;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;
using Wettables;

public class BrickTile : MonoBehaviour, IInteractable, IWettable, INearbyFireReceiver
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private ObjectHealth objectHealth;
    [SerializeField] private PuddleHelper puddleHelper;
    [SerializeField] private TileWetness tileWetness;
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
    public bool OnInteract()
    {
        if (currentPhase == ConstructionPhase.Complete) return false;
        if (interactionHandler.IsCarrying()) return WarningManager.Reject("Put your item down first");
        processHelper.Process();

        if (processHelper.IsCompleted()) CompleteConstruction();
        return true;
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
        if (!NetworkModeHelper.IsNetworked) return; // Singleplayer water comes from particle hits, see TileWetness.
        if (!isFloorTile) return;
        puddleHelper.StartPuddleProcess();
    }
    public void OnElectrocute()
    {
        if (!NetworkModeHelper.IsNetworked)
        {
            tileWetness.Electrify();
            return;
        }
        if (!isFloorTile) return;
        puddleHelper.ElectrocutePuddle();
    }
    #endregion

    #region Fire
    // Brick never catches fire, but enough nearby fires still wear it down.
    public void OnNearbyFire()
    {
        if (currentPhase == ConstructionPhase.Construction) return;
        objectHealth.DealDamage();
    }
    #endregion

    #region UTILITIES
    private void CompleteConstruction()
    {
        gameObject.layer = LayerMask.NameToLayer("Ground");
        currentPhase = ConstructionPhase.Complete;
        tileWetness.SetCanCollect(true);
        connectedConstructor.ReportCompletion();
    }
    public ConstructionPhase GetCurrentPhase()
    {
        return currentPhase;
    }
    #endregion
}
