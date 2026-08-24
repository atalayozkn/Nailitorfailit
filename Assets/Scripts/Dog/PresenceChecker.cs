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
                trackedCarriable = carriable;
                return;
            }
        }

        trackedCarriable = null;
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
                trackedCarriable = interactionHandler.GetCurrentCarriable();
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