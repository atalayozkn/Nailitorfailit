using Interactions;
using ItemScript;
using Mirror;
using UnityEngine;

public class Spawner_MP : NetworkBehaviour, IInteractable
{
    [Header("Settings")]
    public CarriableObject_MP objectPrefab;
    public Transform spawnPoint;
    [SerializeField] private MaterialType materialType = MaterialType.None;

    // Client E'ye basınca bunu çağırır
    public void Interact()
    {
        Debug.Log("Spawner interacted");
        CmdRequestSpawn();
    }

    // Client → Server
    [Command(requiresAuthority = false)]
    private void CmdRequestSpawn()
    {
        SpawnObject();
    }

    // Sadece server'da çalışır
    [Server]
    private void SpawnObject()
    {
        if (objectPrefab == null)
        {
            Debug.LogError("No Prefab assigned!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        CarriableObject_MP instance = Instantiate(objectPrefab, pos, rot);
        instance.InitializeObject(materialType);

        // Tüm client'lara bildir, materialType'ı da gönder
        NetworkServer.Spawn(instance.gameObject);
        RpcInitializeObject(instance.gameObject, materialType);
    }

    // Server → Tüm client'lar: materyal tipini senkronize et
    [ClientRpc]
    private void RpcInitializeObject(GameObject obj, MaterialType type)
    {
        if (obj == null) return;
        CarriableObject_MP carriable = obj.GetComponent<CarriableObject_MP>();
        if (carriable != null)
            carriable.InitializeObject(type);
    }
}