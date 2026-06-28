using Interactions;
using Mirror;
using UnityEngine;
using ItemScript;

public class NetworkSpawner : NetworkBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Safety")]
    [SerializeField] private float spawnCooldown = 0.2f;

    private double lastSpawnTime = -999f;

    public void Interact()
    {
        RequestSpawn();
    }

    public void RequestSpawn()
    {
        if (isServer)
        {
            SpawnObject();
        }
        else
        {
            CmdSpawnObject();
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdSpawnObject()
    {
        SpawnObject();
    }

    [Server]
    private void SpawnObject()
    {
        if (NetworkTime.time - lastSpawnTime < spawnCooldown)
            return;

        if (objectPrefab == null)
        {
            Debug.LogError("Spawner prefab atanmamış!");
            return;
        }

        if (objectPrefab.GetComponent<NetworkIdentity>() == null)
        {
            Debug.LogError("Spawn edilecek prefab üzerinde NetworkIdentity yok!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject instance = Instantiate(objectPrefab, pos, rot);

        NetworkServer.Spawn(instance);

        lastSpawnTime = NetworkTime.time;

        Debug.Log("Network obje spawnlandı: " + objectPrefab.name);
    }
}