using UnityEngine;

public class TrapAbyss : MonoBehaviour
{
    [Header("Respawn Manager")]
    [SerializeField] private PlayerTrapRespawn playerTrapRespawn;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 3f;

    private void Awake()
    {
        ResolveRespawnManager();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        ResolveRespawnManager();

        if (playerTrapRespawn == null)
        {
            Debug.LogWarning("PlayerTrapRespawn bulunamadý!");
            return;
        }

        playerTrapRespawn.RespawnPlayer(respawnDelay);
    }

    private void ResolveRespawnManager()
    {
        if (playerTrapRespawn != null)
            return;

        if (PlayerTrapRespawn.Instance != null)
            playerTrapRespawn = PlayerTrapRespawn.Instance;
        else
            playerTrapRespawn = FindFirstObjectByType<PlayerTrapRespawn>();
    }
}