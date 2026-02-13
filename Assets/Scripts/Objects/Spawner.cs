using Interactions;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;
using ItemScript;
public class NetworkSpawner : NetworkBehaviour, IInteractable
{
    [Header("Settings")]
    // Note: The prefab MUST have a NetworkObject component attached to it
    public CarriableObject objectPrefab;
    public Transform spawnPoint;
    [SerializeField] private MaterialType materialType = MaterialType.None;


    public bool Interact(IPickupable heldItem)
    {
        Debug.Log("inside interact");
        // Sonradan bunu held item varsa kullanamazsın şeklinde yapmak lazım
        RequestSpawn();
        return true;
    }

    public void RequestSpawn()
    {
        // If the Server calls this, we execute logic immediately.
        // If a Client calls this, we send a message (RPC) to the server.
        CmdSpawnObject();
    }


    [Command(requiresAuthority = false)] // Herkes spawn isteyebilsin diye false
    private void CmdSpawnObject()
    {
        if (objectPrefab == null)
        {
            Debug.LogError("No Prefab assigned!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        CarriableObject instance = Instantiate(objectPrefab, pos, rot);
        instance.InitializeObject(this.materialType);

        // Mirror'da DOĞRU spawn yöntemi:
        NetworkServer.Spawn(instance.gameObject);
    }
}
