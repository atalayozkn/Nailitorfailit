using ItemScript;
using UnityEngine;

public class PresenceChecker : MonoBehaviour
{
    // Carriable Settings
    [Header("Carriable Settings")]
    [SerializeField] private LayerMask carriableLayer;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float carriableSearchRadius;

    // Mail-Man Settings
    [Header("Mail-Man Settings")]
    [SerializeField] private LayerMask mailManLayer;
    [SerializeField] private float mailManSearchRadius;
    public bool isDebugMode;

    private CarriableObject_SP trackedCarriable;
    private MailManController trackedMailMan;
    public void SearchForMailMan()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, mailManSearchRadius, mailManLayer);

        if (cols.Length == 0)
        {
            trackedMailMan = null;
            return;
        }

        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<MailManController>(out MailManController mailMan))
            {
                trackedMailMan = mailMan;
                return;
            }
        }

        trackedMailMan = null;
    }
    public void SearchForCarriable()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, carriableSearchRadius, carriableLayer);

        if (cols.Length == 0)
        {
            trackedCarriable = null;
            return;
        }

        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<CarriableObject_SP>(out CarriableObject_SP carriable))
            {
                if (!carriable.IsOccupied() && DogMayTake(carriable))
                {
                    trackedCarriable = carriable;
                }
                else
                {
                    trackedCarriable = null;
                }

                return;
            }
        }

        trackedCarriable = null;
    }

    // In a network session the dog runs on each machine on its own: taking a server-owned item would parent it to the
    // dog's mouth on this machine only (and release it from the player's hands here), while the server and the other
    // players still see the player holding it. Only local-only items (netId 0) may be taken.
    private static bool DogMayTake(CarriableObject_SP carriable)
    {
        if (!NetworkModeHelper.IsNetworked) return true;

        var identity = carriable.GetComponent<Mirror.NetworkIdentity>();
        return identity == null || identity.netId == 0;
    }

    public void SearchForPlayer()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, carriableSearchRadius, playerLayer);

        if (cols.Length == 0)
        {
            trackedCarriable = null;
            return;
        }

        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<PlayerInteractionHandler>(out PlayerInteractionHandler interactionHandler))
            {
                var carriable = interactionHandler.GetCurrentCarriable();
                if (carriable != null && !carriable.IsOccupied() && DogMayTake(carriable))
                {
                    trackedCarriable = carriable;
                }
                else
                {
                    trackedCarriable = null;
                }

                return;
            }
        }

        trackedCarriable = null;
    }
    public CarriableObject_SP GetCurrentCarriable()
    {
        return trackedCarriable;
    }
    public MailManController GetCurrentMailMan()
    {
        return trackedMailMan;
    }
    private void OnDrawGizmosSelected()
    {
        if (!isDebugMode) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, carriableSearchRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, mailManSearchRadius);
    }
}