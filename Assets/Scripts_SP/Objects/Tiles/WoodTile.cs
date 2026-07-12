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
    [SerializeField] private ConstructObject_SP connectedConstructor;
    [SerializeField] private Canvas tileCanvas;
    [SerializeField] private Slider tileSlider;

    [Header("Tile Settings")]
    [SerializeField] private float maxProcessAmount = 100f;
    [SerializeField] private float processPerInteract = 25f;

    [Header("Fire Settings")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int perFireHit = 1;
    [SerializeField] private float fireHitInterval = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onConstructionStarted;
    [SerializeField] private UnityEvent onConstructionProgress;
    [SerializeField] private UnityEvent onConstructionCompleted;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onBurningStartEvent;
    [SerializeField] private UnityEvent onBurningStopEvent;
    [SerializeField] private UnityEvent onFireHitEvent;
    [SerializeField] private UnityEvent onDestroyEvent;

    public InteractableType InteractableType => interactableType;

    private TilePhase currentPhase;
    private float currentProcess;
    private bool startedConstruction;
    private int currentHealth;
    private Coroutine fireRoutine;
    private Coroutine waterRoutine;
    private bool isOnFire = false;
    private bool isWet = false;

    private void OnEnable()
    {
        currentPhase = TilePhase.Construction;
        gameObject.layer = LayerMask.NameToLayer("Interaction");
        currentProcess = 0f;
        startedConstruction = false;
        currentHealth = maxHealth;
        tileCanvas.enabled = true;
        tileSlider.maxValue = maxProcessAmount;
        UpdateUI();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        fireRoutine = null;
        waterRoutine = null;
        isOnFire = false;
        isWet = false;
        tileCanvas.enabled = false;
    }

    #region INTERACTABLE
    public void OnInteract()
    {
        if (currentPhase == TilePhase.Complete)
            return;

        if (!startedConstruction)
        {
            startedConstruction = true;
            onConstructionStarted?.Invoke();
        }

        currentProcess += processPerInteract;
        UpdateUI();
        onConstructionProgress?.Invoke();

        if (currentProcess >= maxProcessAmount)
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
            currentHealth -= perFireHit;
            onFireHitEvent?.Invoke();

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                isOnFire = false;
                Demolish();
                yield break;
            }

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
        currentProcess = maxProcessAmount;
        gameObject.layer = LayerMask.NameToLayer("Ground");
        UpdateUI();
        currentPhase = TilePhase.Complete;
        onConstructionCompleted?.Invoke();
        tileCanvas.enabled = false;
    }
    private void Demolish()
    {
        onDestroyEvent?.Invoke();
        Invoke(nameof(DiscardObject), 1.0f);
    }
    private void DiscardObject()
    {
        connectedConstructor.RequestClosure(gameObject);
    }
    private void UpdateUI()
    {
        tileSlider.value = currentProcess;
    }
    public TilePhase GetCurrentPhase()
    {
        return currentPhase;
    }

    #endregion
}