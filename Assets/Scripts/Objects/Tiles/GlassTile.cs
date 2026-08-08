using Interactions;
using UnityEngine;
using UnityEngine.Events;
using Wettables;
using Breakables;

public class GlassTile : MonoBehaviour, IInteractable, IWettable, IBreakable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private PuddleHelper puddleHelper;
    [SerializeField] private ObjectHealth objectHealth;

    [Header("Settings")]
    [SerializeField] private float pressureDamageDelay = 1.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;
    private ConstructionPhase currentPhase;

    private bool canDealDamage;
    private void OnEnable()
    {
        canDealDamage = true;
        currentPhase = ConstructionPhase.Construction;
        gameObject.layer = LayerMask.NameToLayer("Interaction");
    }

    #region INTERACTABLE
    public void OnInteract()
    {
        if (currentPhase == ConstructionPhase.Complete)
            return;

        processHelper.Process();

        if (processHelper.IsCompleted())
        {
            CompleteConstruction();
        }
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

    #region Breakable
    public void OnPressureApply()
    {
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
