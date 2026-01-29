using Unity.Netcode;
using UnityEngine;

public class SimpleSpawner : NetworkBehaviour
{
    [SerializeField] private Transform spawnPoint;

    public override void OnNetworkSpawn()
    {
        // Only the Server (or Host) manages spawning locations
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerJoined;
        }
    }

    public override void OnNetworkDespawn()
    {
        // Clean up the event listener when object is destroyed
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnPlayerJoined;
        }
    }

    private void OnPlayerJoined(ulong clientId)
    {
        // Get the player object that just spawned
        NetworkObject playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;

        // Move it to the spawn point
        if (playerObject != null && spawnPoint != null)
        {
            playerObject.transform.position = spawnPoint.position;
            playerObject.transform.rotation = spawnPoint.rotation;
        }
    }
}