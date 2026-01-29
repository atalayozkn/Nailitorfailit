using Interactions;
using Unity.Netcode;
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
        SpawnObjectServerRpc();
    }

    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SpawnObjectServerRpc()
    {
        Debug.Log("inside spawn");
        if (objectPrefab == null)
        {
            Debug.LogError("No Prefab assigned!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        // 3. Instantiate the object (Standard Unity stuff)
        CarriableObject instance = Instantiate(objectPrefab, pos, rot);

        // 4. IMPORTANT: Tell Netcode to spawn it across the network
        // This makes it appear on all 4 players' screens.
        var networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
        }
        else
        {
            Debug.LogError("The object you tried to spawn does not have a NetworkObject component!");
        }

        instance.InitializeObject(this.materialType);
    }
}
