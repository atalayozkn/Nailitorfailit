using Interactions;
using ItemScript;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Cutter : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private CarriableType[] acceptedCarriableTypes; 
    [SerializeField] private Transform placementTransform;
    [SerializeField] private Transform spawnTransform;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private Generator_Prototype connectedGenerator;

    [Header("Spawn Settings")]
    [SerializeField] private int spawnCount = 2;
    [SerializeField] private float spawnDelay = 1.0f;
    [SerializeField] private GameObject woodProduct;
    [SerializeField] private GameObject glassProduct;
    [SerializeField] private GameObject brickProduct;

    [Header("Consumption Settings")]
    [SerializeField] private float perProcessCost = 5f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSpawnEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType => interactableType;

    private bool isObjectPlaced;
    private bool isSpawning;

    private CarriableObject_SP placedObject;
    private PlayerInteractionHandler interactionHandler;

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }

    #region INTERACTABLE

    public void OnInteract()
    {
        if (isSpawning) return;

        if (!isObjectPlaced)
        {
            RegisterToStation();
            return;
        }

        if (interactionHandler.IsCarrying())
        {
            return;
        }

        if (!connectedGenerator.HasEnoughEnergy(perProcessCost))
        {
            Debug.Log("No Energy");
            return;
        }

        connectedGenerator.ConsumeEnergy(perProcessCost);

        processHelper.Process();

        if (processHelper.IsCompleted())
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
        CarriableObject_SP carriable = interactionHandler.GetCurrentCarriable();
        if (carriable == null) return;
        if (!carriable.isRawMaterial) return;
        if (!IsCarriableTypeAccepted(carriable.carriableType)) return;

        placedObject = carriable;
        isObjectPlaced = true;
        PlaceObject();
        interactionHandler.ClearCarriedObject();
        processHelper.ResetProcess();
    }
    private bool IsCarriableTypeAccepted(CarriableType type) { return Array.Exists(acceptedCarriableTypes, acceptedType => acceptedType == type); }

    private void PlaceObject()
    {
        if (placedObject == null) return;

        placedObject.transform.SetParent(placementTransform);
        placedObject.transform.localPosition = Vector3.zero;
        placedObject.transform.localRotation = Quaternion.identity;
    }

    private void SpawnObjects()
    {
        if (isSpawning) return;

        if (placedObject == null)
        {
            isObjectPlaced = false;
            return;
        }

        isSpawning = true;

        CarriableObject_SP objectToConsume = placedObject;
        CarriableType objectType = placedObject.carriableType;

        placedObject = null;
        isObjectPlaced = false;

        objectToConsume.OnConsume();

        StartCoroutine(SpawnRoutine(objectType));
    }

    private IEnumerator SpawnRoutine(CarriableType type)
    {
        GameObject objectToSpawn = null;

        switch (type)
        {
            case CarriableType.Wood:
                objectToSpawn = woodProduct;
                break;

            case CarriableType.Brick:
                objectToSpawn = brickProduct;
                break;

            case CarriableType.Glass:
                objectToSpawn = glassProduct;
                break;
        }

        if (objectToSpawn == null)
        {
            isSpawning = false;
            yield break;
        }

        WaitForSeconds wait = new WaitForSeconds(spawnDelay);

        for (int i = 0; i < spawnCount; i++)
        {
            Instantiate(objectToSpawn, spawnTransform.position, Quaternion.identity);
            onSpawnEvent?.Invoke();
            if (i < spawnCount - 1) yield return wait;
        }

        isSpawning = false;
    }

    #endregion
}