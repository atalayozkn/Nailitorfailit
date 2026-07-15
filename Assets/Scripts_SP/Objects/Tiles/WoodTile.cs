using Flammables;
using Interactions;
using ItemScript;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Wettables;

public class WoodTile : MonoBehaviour, IInteractable, IFlammable, IWettable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private ObjectHealth objectHealth;

    [Header("Fire Settings")]
    [SerializeField] private float fireHitInterval = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onBurningStartEvent;
    [SerializeField] private UnityEvent onBurningStopEvent;

    public InteractableType InteractableType => interactableType;
    private ConstructionPhase currentPhase;

    private Coroutine fireRoutine;
    private Coroutine waterRoutine;

    private bool isOnFire = false;
    private bool isWet = false;

    private void OnEnable()
    {
        currentPhase = ConstructionPhase.Construction;
        gameObject.layer = LayerMask.NameToLayer("Interaction");
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        fireRoutine = null;
        waterRoutine = null;
        isOnFire = false;
        isWet = false;
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

    #region FIRE RELATED
    public void OnFireStart()
    {
        if (isOnFire) return;
        isOnFire = true;

        if (fireRoutine != null) return;
        fireRoutine = StartCoroutine(FireRoutine());
    }
    private IEnumerator FireRoutine()
    {
        while (true)
        {
            objectHealth.DealDamage();
            yield return new WaitForSeconds(fireHitInterval);
            isOnFire = false;
        }
    }
    public void OnFireStop()
    {
        StopCoroutine(fireRoutine);
        isOnFire = false;
    }
    #endregion

    #region WATER RELATED
    public void OnWaterContact()
    {
        if (isWet) return;
        if (waterRoutine != null) return;
        //waterRoutine = StartCoroutine(WaterRoutine());
    }
    private IEnumerator WaterRoutine()
    {
        while (true)
        {

            yield return null;
        }
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