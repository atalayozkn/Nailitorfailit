using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class Mixer : MonoBehaviour, IInteractable
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

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }

    private void OnEnable()
    {
        ClearSlots();
    }

    #region Interactable

    public void OnInteract()
    {
        CarriableObject_SP carriable = interactionHandler.GetCurrentCarriable();
        if (carriable == null) return;

        CarriableType carriableType = interactionHandler.GetCurrentCarriableType();
        if (!AcceptsCarriableType(carriableType)) return;

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
            return;
        }

        PlaceObject(carriable);
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
    public void CompareFormula()
    {
        CarriableObject_SP[] storedObjects = GetStoredObjects();

        // We cannot compare an incomplete mixer.
        if (firstCarriable == null || secondCarriable == null || thirdCarriable == null)
        {
            DropStoredItems();
            return;
        }

        MixerFormulaSO matchedFormula = FindMatchingFormula(storedObjects);

        if (matchedFormula != null) ProduceResult(matchedFormula);
        else DropStoredItems();
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

    private void ProduceResult(MixerFormulaSO formula)
    {
        if (formula == null) return;
        //Check for Energy
        if (!connectedGenerator.HasEnoughEnergy(processCost)) return;

        // First remove the ingredients.
        DestroyStoredItems();
        // Spawn the result.
        SpawnProduct(formula);
    }

    private void SpawnProduct(MixerFormulaSO formula)
    {
        if (formula == null || formula.ProductPrefab == null) return;
        Instantiate(formula.ProductPrefab, productSpawnPoint.position, productSpawnPoint.rotation);
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
        DropCarriable(firstCarriable);
        DropCarriable(secondCarriable);
        DropCarriable(thirdCarriable);

        firstCarriable = null;
        secondCarriable = null;
        thirdCarriable = null;

        uiHelper.ClearImages();
        // UI update can happen here.
    }

    private void DropCarriable(CarriableObject_SP carriable)
    {
        if (carriable == null) return;
        // Make the object visible again.
        carriable.SetVisuals(true);
        // Detach from mixer.
        carriable.transform.SetParent(null);
    }

    private void ClearSlots()
    {
        firstCarriable = null;
        secondCarriable = null;
        thirdCarriable = null;

        // UI update can happen here.
    }
}