using Interactions;
using ItemScript;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Cutter : MonoBehaviour, IInteractable, ILookAtInfo
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private CarriableType[] acceptedCarriableTypes; 
    [SerializeField] private Transform placementTransform;
    [SerializeField] private Transform spawnTransform;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private Generator_Prototype connectedGenerator;

    [Header("Tutorial Settings")]
    [SerializeField] private bool isTutorial = false;
    [SerializeField] private TutorialObjective objective;

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

    public bool OnInteract()
    {
        if (isSpawning) return false;

        // Loading doesn't need power, only the processing does.
        if (!isObjectPlaced) return RegisterToStation();

        if (interactionHandler.IsCarrying()) return false;

        if (!connectedGenerator.HasEnoughEnergy(perProcessCost)) return WarningManager.Reject("No power! Refuel the generator", true);

        connectedGenerator.ConsumeEnergy(perProcessCost);

        processHelper.Process();
        VfxManager.Play(VfxId.CutterCutting, placementTransform.position);

        if (processHelper.IsCompleted())
        {
            SpawnObjects();
        }

        return true;
    }

    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        const string name = "Cutter";
        if (isSpawning) return LookAtInfo.Blocked(name, "The cutter is busy");
        if (!isObjectPlaced)
        {
            if (carried == null) return LookAtInfo.Blocked(name, "Bring raw material");
            if (!carried.isRawMaterial) return LookAtInfo.Blocked(name, "This is already processed");
            if (!IsCarriableTypeAccepted(carried.carriableType)) return LookAtInfo.Blocked(name, "Can not process this");
            return LookAtInfo.Ready(name, $"Load {LookAtText.ItemName(carried)}");
        }
        if (carried != null) return LookAtInfo.Blocked(name, "Already loaded");
        if (connectedGenerator != null && !connectedGenerator.HasEnergyFor(perProcessCost)) return LookAtInfo.Blocked(name, "No power! Refuel the generator");
        return LookAtInfo.Ready(name, "Cut");
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

    private bool RegisterToStation()
    {
        CarriableObject_SP carriable = interactionHandler.GetCurrentCarriable();
        if (carriable == null) return false;
        if (!carriable.isRawMaterial) return false;
        if (!IsCarriableTypeAccepted(carriable.carriableType)) return false;

        placedObject = carriable;
        carriable.SetOccupied();
        isObjectPlaced = true;
        PlaceObject();
        interactionHandler.ClearCarriedObject();
        processHelper.ResetProcess();
        return true;
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
        if (isTutorial) objective.CompleteCondition();
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
            VfxManager.Play(VfxId.ProductOut, spawnTransform.position);
            onSpawnEvent?.Invoke();
            if (i < spawnCount - 1) yield return wait;
        }

        isSpawning = false;
    }

    #endregion
}
