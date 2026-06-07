using Interactions;
using UnityEngine;
using ItemScript;

public class NetworkSpawner_SP : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    public CarriableObject_SP objectPrefab;
    public Transform spawnPoint;

    [SerializeField] private MaterialType materialType = MaterialType.None;

    public void Interact()
    {
        Debug.Log("Spawner çalýþtý");
        RequestSpawn();
    }

    public void RequestSpawn()
    {
        SpawnObject();
    }

    private void SpawnObject()
    {
        if (objectPrefab == null)
        {
            Debug.LogError("No Prefab assigned!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        CarriableObject_SP instance = Instantiate(objectPrefab, pos, rot);
        instance.InitializeObject(materialType);
    }
}