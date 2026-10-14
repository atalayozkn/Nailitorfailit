using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class ObjectSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType;
    public InteractableType InteractableType => interactableType;
    [Header("Settings")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private float interactCooldown;

    [Header("Tutorial Settings")]
    [SerializeField] private bool isTutorial = false;
    [SerializeField] private TutorialObjective objective;

    [Header("Safety")]
    [SerializeField] private int maxCount = 15;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onInteractEvent;

    private int spawnedObjectCount = 0;
    private bool isOnCooldown = false;
    private PlayerInteractionHandler interactionHandler;
    private void Awake()
    {
        interactionHandler = FindAnyObjectByType<PlayerInteractionHandler>();
    }
    public void OnInteract()
    {
        if (isTutorial) objective.CompleteCondition();

        if (interactionHandler.IsCarrying() || isOnCooldown)
        {
            //Request Impossible Action SFX
            return;
        }

        isOnCooldown = true;
        onInteractEvent?.Invoke();
        SpawnObject();
        Invoke(nameof(ReverseCooldown), interactCooldown);
    }
    private void ReverseCooldown()
    {
        isOnCooldown = false;
    }
    public void OnHoverOn()
    {
        onHoverOnEvent?.Invoke();
    }
    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }
    private void IncrementCounter()
    {
        spawnedObjectCount ++;
        if (spawnedObjectCount > maxCount)
        {
            spawnedObjectCount = maxCount;
        }
    }
    public void ReduceCounter()
    {
        spawnedObjectCount --;
        if (spawnedObjectCount < 0)
        {
            spawnedObjectCount = 0;
        }
    }
    private void SpawnObject()
    {
        if (objectPrefab == null || spawnedObjectCount >= maxCount) return;

        var instantiatedObject = Instantiate(objectPrefab, null);
        if (instantiatedObject != null && instantiatedObject.TryGetComponent<CarriableObject_SP>(out CarriableObject_SP carriable))
        {
            carriable.OnInteract();
        }

        IncrementCounter();

        if (instantiatedObject.TryGetComponent<ISpawnable>(out var spawnable))
        {
            spawnable.OnSpawn(gameObject);
        }
    }
}