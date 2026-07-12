using Interactions;
using ItemScript;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Station : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private Transform placementTransform;
    [SerializeField] private Transform spawnTransform;
    [SerializeField] private GameObject spawnedObjectPrefab;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Canvas progressCanvas;

    [Header("Settings")]
    [SerializeField] private float maxProcessAmount = 100;
    [SerializeField] private float perProcessAmount = 25f;
    [SerializeField] private int spawnCount = 2;
    [SerializeField] private float spawnDelay = 1.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSpawnEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;

    private bool isObjectPlaced = false;
    private float currentProcess = 0f;
    private CarriableObject_SP placedObject;
    private PlayerInteract_SP interactionHandler;

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteract_SP>();
    }
    private void OnEnable()
    {
        currentProcess = 0f;
        progressSlider.maxValue = maxProcessAmount;
        progressCanvas.enabled = false;
        UpdateUI();
    }

    private void OnDisable()
    {
        currentProcess = 0f;
    }

    #region INTERACTABLE RELATED
    public void OnInteract()
    {
        if (!isObjectPlaced)
        {
            isObjectPlaced = true;
            RegisterToStation();
        }
        Process();
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

    private void RegisterToStation()
    {
        CarriableObject_SP carriable = interactionHandler.GetCurrentCarriable();
        placedObject = carriable;
        PlaceObject();
        interactionHandler.ClearCarriedObject();
    }
    private void PlaceObject()
    {
        if (placedObject == null) return;
        placedObject.transform.SetParent(placementTransform);
        placedObject.transform.localPosition = Vector3.zero;
        placedObject.transform.localRotation = Quaternion.identity;
        progressCanvas.enabled = true;
    }
    private void Process()
    {
        currentProcess += perProcessAmount;
        UpdateUI();

        if (currentProcess >= maxProcessAmount)
        {
            currentProcess = 0f;
            UpdateUI();
            SpawnObjects();
        }
    }
    private void SpawnObjects()
    {
        placedObject?.OnConsume();
        progressCanvas.enabled = false;
        StartCoroutine(SpawnRoutine());
    }
    private IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Instantiate(spawnedObjectPrefab, spawnTransform.position, Quaternion.identity);

            onSpawnEvent?.Invoke();
            if (i < spawnCount - 1) yield return new WaitForSeconds(spawnDelay);
        }
    }

    #region UTILITIES
    private void UpdateUI()
    {
        progressSlider.value = currentProcess;
    }

    #endregion
}
