using Flammables;
using UnityEngine;
using UnityEngine.Events;

public class TrapElectricity : MonoBehaviour
{
    [Header("Respawn Manager")]
    [SerializeField] private PlayerTrapRespawn playerTrapRespawn;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Box Settings")]
    [SerializeField] private Vector3 hitDimension;
    [SerializeField] private Transform muzzle;
    [SerializeField] private LayerMask electricMask;

    [Header("Electricity Respawn")]
    [SerializeField] private float electricStunDuration = 3f;
    [SerializeField] private float electricRespawnDelay = 3f;

    [Header("Events")]
    [SerializeField] private UnityEvent onTrapTriggeredEvent;
    [SerializeField] private UnityEvent onElectricTrapEvent;

    public void OnTrigger()
    {
        TryElectrocute();
    }

    private void Awake()
    {
        ResolveRespawnManager();
    }

    private void TryElectrocute()
    {
        if (muzzle == null)
            return;

        Collider[] colliders = Physics.OverlapBox(
            muzzle.position,
            hitDimension,
            Quaternion.identity,
            electricMask
        );

        if (colliders.Length == 0)
            return;

        bool playerHit = false;

        foreach (var col in colliders)
        {
            if (col.gameObject.TryGetComponent<IFlammable>(out IFlammable flammable))
            {
                flammable.OnFireStart();
            }

            if (col.CompareTag(playerTag))
            {
                playerHit = true;
            }
        }

        if (!playerHit)
            return;

        ResolveRespawnManager();

        if (playerTrapRespawn == null)
        {
            Debug.LogWarning("PlayerTrapRespawn bulunamadý!");
            return;
        }

        if (playerTrapRespawn.IsRespawning)
            return;

        onTrapTriggeredEvent?.Invoke();
        onElectricTrapEvent?.Invoke();

        playerTrapRespawn.RespawnPlayerAfterDelay(
            electricStunDuration,
            electricRespawnDelay
        );
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

    private void OnDrawGizmosSelected()
    {
        if (muzzle == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(muzzle.position, hitDimension * 2f);
    }
}