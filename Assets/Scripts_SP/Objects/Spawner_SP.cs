using Interactions;
using UnityEngine;
using UnityEngine.Events;

public class ObjectSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType;
    public InteractableType InteractableType => interactableType;
    [Header("Settings")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Safety")]
    [SerializeField] private int maxCount = 15;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onInteractEvent;

    private int spawnedObjectCount = 0;
    public void OnInteract()
    {
        onInteractEvent?.Invoke();
        SpawnObject();
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
        if (objectPrefab == null)
        {
            Debug.LogError("Spawner Prefab Missing");
            return;
        }

        if (spawnedObjectCount >= maxCount) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        var instantiatedObject = Instantiate(objectPrefab, pos, rot);
        IncrementCounter();

        if (instantiatedObject.TryGetComponent<ISpawnable>(out var spawnable))
        {
            spawnable.OnSpawn(gameObject);
        }
    }
}