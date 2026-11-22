using System.Collections.Generic;
using UnityEngine;

// Shop item for the versus mode. Dropped near a dog it distracts that dog (see VersusDogGuard).
// Sits on a normal carriable, so buying, carrying and dropping all use the existing code.
public class VersusBait : MonoBehaviour
{
    private static readonly List<VersusBait> All = new List<VersusBait>();

    private Collider ownCollider;

    // A dog is already going for it (local to the machine that runs that dog).
    public bool Claimed { get; set; }

    // On the ground, not in anyone's hands.
    public bool IsLoose => transform.parent == null && (ownCollider == null || ownCollider.enabled);

    private void Awake()
    {
        ownCollider = GetComponent<Collider>();
    }

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);

    public static VersusBait FindLooseNear(Vector3 position, float radius)
    {
        VersusBait nearest = null;
        float nearestSqr = radius * radius;

        foreach (VersusBait bait in All)
        {
            if (bait == null || bait.Claimed || !bait.IsLoose) continue;

            Vector3 offset = bait.transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > nearestSqr) continue;

            nearestSqr = offset.sqrMagnitude;
            nearest = bait;
        }

        return nearest;
    }

    public void Consume()
    {
        if (TryGetComponent(out CarriableObject_MP networked) && NetworkModeHelper.IsNetworked) networked.RequestConsume(null);
        else Destroy(gameObject);
    }
}
