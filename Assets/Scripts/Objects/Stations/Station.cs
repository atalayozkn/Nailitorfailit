using Interactions;
using ItemScript;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Station : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private CarriableType acceptedCarriableType;
    [SerializeField] private Transform placementTransform;
    [SerializeField] private Transform spawnTransform;
    [SerializeField] private GameObject spawnedObjectPrefab;
    [SerializeField] private InteractionProcessHelper processHelper;
    [SerializeField] private Generator_SP generator;

    [Header("Spawn Settings")]
    [SerializeField] private int spawnCount = 2;
    [SerializeField] private float spawnDelay = 1.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSpawnEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    public InteractableType InteractableType =>
        interactableType;

    private bool isObjectPlaced;
    private bool isSpawning;

    private CarriableObject_SP placedObject;
    private PlayerInteractionHandler interactionHandler;

    private void Awake()
    {
        interactionHandler =
            FindFirstObjectByType<PlayerInteractionHandler>();
    }

    #region INTERACTABLE

    public void OnInteract()
    {
        if (isSpawning)
        {
            return;
        }

        if (!isObjectPlaced)
        {
            RegisterToStation();
            return;
        }

        if (!CanStationOperate())
        {
            return;
        }

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

    #region GENERATOR

    private bool CanStationOperate()
    {
        if (generator == null)
        {
            Debug.LogWarning(
                $"{name}: Station'a Generator referansı verilmemiş."
            );

            return false;
        }

        if (!generator.HasPower())
        {
            Debug.Log(
                $"{name}: Jeneratörde enerji yok."
            );

            return false;
        }

        return true;
    }

    #endregion

    #region STATION

    private void RegisterToStation()
    {
        CarriableObject_SP carriable =
            interactionHandler.GetCurrentCarriable();

        if (carriable == null)
        {
            return;
        }

        if (carriable.carriableType !=
            acceptedCarriableType)
        {
            return;
        }

        placedObject = carriable;
        isObjectPlaced = true;

        PlaceObject();

        interactionHandler.ClearCarriedObject();
        processHelper.ResetProcess();
    }

    private void PlaceObject()
    {
        if (placedObject == null)
        {
            return;
        }

        placedObject.transform.SetParent(
            placementTransform
        );

        placedObject.transform.localPosition =
            Vector3.zero;

        placedObject.transform.localRotation =
            Quaternion.identity;
    }

    private void SpawnObjects()
    {
        if (isSpawning)
        {
            return;
        }

        if (placedObject == null)
        {
            isObjectPlaced = false;
            return;
        }

        isSpawning = true;

        CarriableObject_SP objectToConsume =
            placedObject;

        placedObject = null;
        isObjectPlaced = false;

        objectToConsume.OnConsume();

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        WaitForSeconds wait =
            new WaitForSeconds(spawnDelay);

        for (int i = 0; i < spawnCount; i++)
        {
            Instantiate(
                spawnedObjectPrefab,
                spawnTransform.position,
                Quaternion.identity
            );

            onSpawnEvent?.Invoke();

            if (i < spawnCount - 1)
            {
                yield return wait;
            }
        }

        isSpawning = false;
    }

    #endregion
}