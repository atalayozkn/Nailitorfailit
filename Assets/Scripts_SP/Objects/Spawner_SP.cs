using Interactions;
using UnityEngine;

public class NetworkSpawner_SP : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Safety")]
    [SerializeField] private float spawnCooldown = 0.2f;

    private float lastSpawnTime = -999f;

    public void Interact()
    {
        RequestSpawn();
    }

    public void RequestSpawn()
    {
        SpawnObject();
    }

    private void SpawnObject()
    {
        if (Time.time - lastSpawnTime < spawnCooldown)
            return;

        if (objectPrefab == null)
        {
            Debug.LogError("Spawner prefab atanmamýþ!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        Instantiate(objectPrefab, pos, rot);

        lastSpawnTime = Time.time;

        Debug.Log("Obje spawnlandý: " + objectPrefab.name);
    }
}