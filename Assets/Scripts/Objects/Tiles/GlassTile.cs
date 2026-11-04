using Breakables;
using Flammables;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;
using Wettables;

public class GlassTile : MonoBehaviour, IInteractable, IWettable, IBreakable, INearbyFireReceiver
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private PuddleHelper puddleHelper;
    [SerializeField] private TileWetness tileWetness;
    [SerializeField] private ObjectHealth objectHealth;
    [SerializeField] private Constructor connectedConstructor;
    [SerializeField] private bool isFloorTile;

    [Header("Settings")]
    [SerializeField] private float pressureDamageDelay = 1.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;
    private ConstructionPhase currentPhase;

    private bool canDealDamage;
    private PlayerInteractionHandler interactionHandler;
    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }
    private void OnEnable()
    {
        canDealDamage = true;
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

    #region Breakable
    public void OnPressureApply()
    {
        if (!isFloorTile && NetworkModeHelper.IsNetworked) return;
        if (currentPhase == ConstructionPhase.Construction) return;
        if (!canDealDamage) return;
        canDealDamage = false;
        objectHealth.DealDamage();
        Invoke(nameof(ReverseDamage), pressureDamageDelay);
    }

    private void ReverseDamage()
    {
        canDealDamage = true;
    }

    #endregion

    #region Fire
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
