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
    [SerializeField] private CarriableType acceptedCarriableType;
    [SerializeField] private Transform placementTransform;
    [SerializeField] private Transform spawnTransform;
    [SerializeField] private GameObject spawnedObjectPrefab;
    [SerializeField] private InteractionProcessHelper processHelper;

    [Header("Spawn Settings")]
    [SerializeField] private int spawnCount = 2;
    [SerializeField] private float spawnDelay = 1.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSpawnEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    public InteractableType InteractableType => interactableType;

    private bool isObjectPlaced = false;
    private CarriableObject_SP placedObject;
    private PlayerInteractionHandler interactionHandler;

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }

    #region INTERACTABLE RELATED
    public void OnInteract()
    {
        if (!isObjectPlaced) //Obje henüz istasyona yerleştirilmedi ise yerleştir.
        {
            RegisterToStation();
            return;
        }

        processHelper.Process(); //Süreci ilerlet

        if (processHelper.IsCompleted()) //Süreç tamamlandı ise yeni objeleri spawnla.
        {
            SpawnObjects();
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

    #region UTILITIES
    private void RegisterToStation()
    {
        if (interactionHandler.GetCurrentCarriableType() != acceptedCarriableType) return;

        isObjectPlaced = true;

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
    }
    private void SpawnObjects()
    {
        placedObject?.OnConsume();
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

    #endregion
}
