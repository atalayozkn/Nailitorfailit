using ItemScript;
using Unity.VisualScripting;
using UnityEngine;

public class PresenceChecker : MonoBehaviour
{
    // Carriable Settings
    [Header("Carriable Settings")]
    [SerializeField] private LayerMask carriableLayer;
    [SerializeField] private float carriableSearchRadius;

    // Mail-Man Settings
    [Header("Mail-Man Settings")]
    [SerializeField] private LayerMask mailManLayer;
    [SerializeField] private float mailManSearchRadius;
    public bool isDebugMode;

    private CarriableObject_SP trackedCarriable;
    private MailManController trackedMailMan;
    public Transform SearchForMailMan()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, mailManSearchRadius, mailManLayer);

        if (cols.Length == 0)
        {
            trackedMailMan = null;
            return null;
        }

        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<MailManController>(out MailManController mailMan))
            {
                trackedMailMan = mailMan;
                return mailMan.transform;
            }
        }

        trackedMailMan = null;
        return null;
    }
    public Transform SearchForCarriable()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, carriableSearchRadius, carriableLayer);

        if (cols.Length == 0)
        {
            trackedCarriable = null;
            return null;
        }

        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<CarriableObject_SP>(out CarriableObject_SP carriable))
            {
                trackedCarriable = carriable;
                return carriable.transform;
            }
        }

        trackedCarriable = null;
        return null;
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