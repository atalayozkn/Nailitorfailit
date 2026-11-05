using Flammables;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;
using Wettables;

public class BrickTile : MonoBehaviour, IInteractable, ILookAtInfo, IWettable, INearbyFireReceiver, IBuildableTile
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
    public float BuildProgress => processHelper.NormalizedProgress;
    public bool IsBuildComplete => currentPhase == ConstructionPhase.Complete;
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
        if (interactionHandler.IsCarrying()) return false;
        processHelper.Process();
        VfxManager.Play(VfxId.TileNail, transform.position);

        if (processHelper.IsCompleted()) CompleteConstruction();
        return true;
    }
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        if (currentPhase == ConstructionPhase.Complete) return default;
        const string name = "Brick Tile";
        if (carried != null) return LookAtInfo.Blocked(name, "Put your item down first");
        return LookAtInfo.Ready(name, "Nail down");
    }
    public void OnHoverOn()
    {
        if (connectedConstructor != null) connectedConstructor.SetBlueprintHovered(true);
        onHoverOnEvent?.Invoke();
    }
    public void OnHoverOff()
    {
        if (connectedConstructor != null) connectedConstructor.SetBlueprintHovered(false);
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
        VfxManager.Play(VfxId.TileFinished, transform.position);
        connectedConstructor.ReportCompletion();
    }
    public ConstructionPhase GetCurrentPhase()
    {
        return currentPhase;
    }
    #endregion
}
