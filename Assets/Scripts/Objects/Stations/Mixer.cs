using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class Mixer : MonoBehaviour, IInteractable, ILookAtInfo
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType = InteractableType.Station;
    [SerializeField] private Generator_Prototype connectedGenerator;
    [SerializeField] private MixerUIHelper uiHelper;
    [SerializeField] private Transform placementTransform;

    [Header("Settings")]
    [Tooltip("Optional. Leave empty if the mixer accepts any CarriableType.")]
    [SerializeField] private CarriableType[] acceptedCarriableTypes;
    [SerializeField] private float processCost;

    [Header("Formulas")]
    [SerializeField] private MixerFormulaSO[] formulas;

    [Header("Product")]
    [SerializeField] private Transform productSpawnPoint;
    [Header("Events")]
    [SerializeField] private UnityEvent onSpawnEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;

    private PlayerInteractionHandler interactionHandler;

    // We store the actual objects.
    private CarriableObject_SP firstCarriable;
    private CarriableObject_SP secondCarriable;
    private CarriableObject_SP thirdCarriable;

    // The handle glimmers once the mixer is full, so the player sees what to press next. It is its own effect
    // (not the hover glow), so hovering the handle or the mixer next to it never switches it off. It stays on
    // until the handle is used, and comes back the next time the mixer is filled.
    private AttentionGlimmer handleGlimmer;
    private bool handleUsed;
    private bool remoteFull; // multiplayer client: the slots live on the server, Mixer_MP reports them here

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();

        MixerHandle handle = GetComponentInChildren<MixerHandle>(true);
        if (handle != null)
        {
            handleGlimmer = handle.GetComponent<AttentionGlimmer>();
            if (handleGlimmer == null) handleGlimmer = handle.gameObject.AddComponent<AttentionGlimmer>();
        }
    }

    // Polled rather than hooked: multiplayer fills and empties the slots without going through these methods.
    private void Update()
    {
        bool full = remoteFull || (firstCarriable != null && secondCarriable != null && thirdCarriable != null);
        if (!full) handleUsed = false;
        bool glimmer = full && !handleUsed;
        if (handleGlimmer != null && glimmer != handleGlimmer.IsOn) handleGlimmer.SetOn(glimmer);
    }

    // Called when a player pulls the handle (single player and multiplayer).
    public void NotifyHandleUsed() => handleUsed = true;

    // Multiplayer client: whether all three synced slots are filled.
    public void SetRemoteFull(bool full) => remoteFull = full;

    private void OnEnable()
    {
        ClearSlots();
    }

    #region Interactable

    public bool OnInteract()
    {
        CarriableObject_SP carriable = interactionHandler.GetCurrentCarriable();
        if (carriable == null) return false;

        CarriableType carriableType = interactionHandler.GetCurrentCarriableType();
        if (!AcceptsCarriableType(carriableType)) return false;

        if (firstCarriable == null)
        {
            firstCarriable = carriable;
            uiHelper.DisplayItem(1, carriableType);
        }
        else if (secondCarriable == null)
        {
            secondCarriable = carriable;
            uiHelper.DisplayItem(2, carriableType);
        }
        else if (thirdCarriable == null)
        {
            thirdCarriable = carriable;
            uiHelper.DisplayItem(3, carriableType);
        }
        else
        {
            return false;
        }

        PlaceObject(carriable);
        return true;
    }
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        const string name = "Mixer";
        if (carried == null) return LookAtInfo.Blocked(name, "Bring ingredients");
        if (!AcceptsCarriableType(carried.carriableType)) return LookAtInfo.Blocked(name, "Does not accept this");
        if (firstCarriable != null && secondCarriable != null && thirdCarriable != null) return LookAtInfo.Blocked(name, "The mixer is full");
        return LookAtInfo.Ready(name, $"Add {LookAtText.ItemName(carried)}");
    }
    // Mirrors CompareFormula without running it.
    public LookAtInfo GetHandleLookAtInfo()
    {
        const string name = "Mixer Handle";
        if (firstCarriable == null && secondCarriable == null && thirdCarriable == null) return LookAtInfo.Blocked(name, "The mixer is empty");
        if (firstCarriable == null || secondCarriable == null || thirdCarriable == null) return LookAtInfo.Ready(name, "Eject ingredients");
        if (FindMatchingFormula(GetStoredObjects()) != null && connectedGenerator != null && !connectedGenerator.HasEnergyFor(processCost)) return LookAtInfo.Blocked(name, "No power! Refuel the generator");
        return LookAtInfo.Ready(name, "Mix");
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

    private bool AcceptsCarriableType(CarriableType type)
    {
        // Empty list = accept everything.
        if (acceptedCarriableTypes == null || acceptedCarriableTypes.Length == 0) return true;

        for (int i = 0; i < acceptedCarriableTypes.Length; i++)
        {
            if (acceptedCarriableTypes[i] == type) return true;
        }

        return false;
    }
    private void PlaceObject(CarriableObject_SP carriable)
    {
        carriable.transform.SetParent(placementTransform);
        carriable.transform.localPosition = Vector3.zero;
        carriable.transform.localRotation = Quaternion.identity;
        carriable.SetVisuals(false);
        interactionHandler.ClearCarriedObject();
    }
    private CarriableObject_SP[] GetStoredObjects()
    {
        return new CarriableObject_SP[]
        {
            firstCarriable,
            secondCarriable,
            thirdCarriable
        };
    }
    public bool CompareFormula()
    {
        CarriableObject_SP[] storedObjects = GetStoredObjects();

        // An empty mixer has nothing to do.
        if (firstCarriable == null && secondCarriable == null && thirdCarriable == null) return false;

        // We cannot compare an incomplete mixer.
        if (firstCarriable == null || secondCarriable == null || thirdCarriable == null)
        {
            WarningManager.Show("The mixer needs 3 ingredients");
            DropStoredItems();
            return true;
        }

        MixerFormulaSO matchedFormula = FindMatchingFormula(storedObjects);

        if (matchedFormula != null) return ProduceResult(matchedFormula);

        VfxManager.Play(VfxId.MixerFail, placementTransform.position);
        WarningManager.Show("No recipe for these ingredients");
        DropStoredItems();
        return true;
    }

    private MixerFormulaSO FindMatchingFormula(CarriableObject_SP[] storedObjects)
    {
        if (formulas == null) return null;

        for (int i = 0; i < formulas.Length; i++)
        {
            MixerFormulaSO formula = formulas[i];
            if (formula == null) continue;
            if (formula.Matches(storedObjects)) return formula;
        }

        return null;
    }

    private bool ProduceResult(MixerFormulaSO formula)
    {
        if (formula == null) return false;
        //Check for Energy
        if (!connectedGenerator.HasEnoughEnergy(processCost)) return WarningManager.Reject("No power! Refuel the generator");
        connectedGenerator.ConsumeEnergy(processCost);

        // First remove the ingredients.
        DestroyStoredItems();
        // Spawn the result.
        SpawnProduct(formula);
        return true;
    }

    private void SpawnProduct(MixerFormulaSO formula)
    {
        if (formula == null || formula.ProductPrefab == null) return;
        Instantiate(formula.ProductPrefab, productSpawnPoint.position, productSpawnPoint.rotation);
        onSpawnEvent?.Invoke();
        VfxManager.Play(VfxId.MixerDone, productSpawnPoint.position);
    }
    private void DestroyStoredItems()
    {
        DestroyCarriable(firstCarriable);
        DestroyCarriable(secondCarriable);
        DestroyCarriable(thirdCarriable);

        firstCarriable = null;
        secondCarriable = null;
        thirdCarriable = null;

        uiHelper.ClearImages();

        // UI update can happen here.
    }
    private void DestroyCarriable(CarriableObject_SP carriable)
    {
        if (carriable == null) return;
        Destroy(carriable.gameObject);
    }

    private void DropStoredItems()
    {
        // Side by side: items collide with each other now, and three dropped on one spot would shove each other away.
        DropCarriable(firstCarriable, -1);
        DropCarriable(secondCarriable, 0);
        DropCarriable(thirdCarriable, 1);

        firstCarriable = null;
        secondCarriable = null;
        thirdCarriable = null;

        uiHelper.ClearImages();
        // UI update can happen here.
    }

    private const float DropSpacing = 0.35f;

    private void DropCarriable(CarriableObject_SP carriable, int side)
    {
        if (carriable == null) return;
        // Make the object visible again.
        carriable.SetVisuals(true);
        carriable.transform.position += placementTransform.right * (side * DropSpacing);
        // Detach from mixer.
        carriable.OnDrop();
    }

    private void ClearSlots()
    {
        firstCarriable = null;
        secondCarriable = null;
        thirdCarriable = null;

        // UI update can happen here.
    }
}
