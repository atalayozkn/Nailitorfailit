using System.Collections.Generic;
using UnityEngine;

// Keeps a just-released item (thrown or dropped) from colliding with the player who let go of it.
// The item leaves from the hand, inside the arm and next to the body, and the Interaction layer collides with the
// Player layer. Without this, the item sometimes hit the thrower's own limbs or capsule on release, which ate or
// deflected part of the throw impulse (depends on the hand pose that frame, so only "sometimes").
// Re-enabling a collider resets its ignore state and PlayerActiveRagdoll re-allows limb collisions every 0.1 s for
// colliders that left the player, so the ignore is re-applied every physics step, late in the order, until the item
// has cleared the player's colliders (or a timeout), then normal collisions come back.
[DefaultExecutionOrder(1000)]
public class ThrowReleaseGuard : MonoBehaviour
{
    const float MinDuration = 0.2f;
    const float MaxDuration = 1f;
    const float ClearMargin = 0.1f;

    private readonly List<Collider> itemColliders = new();
    private readonly List<Collider> holderColliders = new();
    private float startTime;

    // Call right after the item's colliders are switched back on and it is unparented.
    public static void Protect(GameObject item, Component holder)
    {
        if (item == null || holder == null) return;
        var guard = item.GetComponent<ThrowReleaseGuard>();
        if (guard == null) guard = item.AddComponent<ThrowReleaseGuard>();
        guard.Begin(HolderRoot(holder));
    }

    // The whole player (capsule and every limb): the higher of the holder and its ragdoll.
    private static Transform HolderRoot(Component holder)
    {
        var ragdoll = holder.GetComponentInParent<PlayerActiveRagdoll>();
        return ragdoll != null ? ragdoll.transform : holder.transform;
    }

    private void Begin(Transform holderRoot)
    {
        Restore(); // a previous release still running
        startTime = Time.time;

        GetComponentsInChildren(true, itemColliders);
        holderRoot.GetComponentsInChildren(true, holderColliders);
        holderColliders.RemoveAll(c => c == null || c.isTrigger || c.transform.IsChildOf(transform));
        enabled = true;
        Apply(true);
    }

    private void FixedUpdate()
    {
        // Picked up or placed again: the carrier owns the collider state now (carried items have colliders off).
        if (transform.parent != null)
        {
            itemColliders.Clear();
            holderColliders.Clear();
            enabled = false;
            return;
        }

        float age = Time.time - startTime;
        if (age >= MaxDuration || (age >= MinDuration && Clear()))
        {
            Restore();
            enabled = false;
            return;
        }
        Apply(true);
    }

    private void OnDisable() => Restore();

    // No enabled item collider overlaps any of the holder's colliders any more.
    private bool Clear()
    {
        foreach (Collider item in itemColliders)
        {
            if (item == null || !item.enabled) continue;
            Bounds a = item.bounds;
            a.Expand(ClearMargin);
            foreach (Collider other in holderColliders)
                if (other != null && other.enabled && a.Intersects(other.bounds)) return false;
        }
        return true;
    }

    private void Apply(bool ignore)
    {
        // IgnoreCollision needs both colliders active (and a switched-off collider forgets it anyway).
        foreach (Collider item in itemColliders)
        {
            if (!Active(item) || item.isTrigger) continue;
            foreach (Collider other in holderColliders)
                if (Active(other)) Physics.IgnoreCollision(item, other, ignore);
        }
    }

    private static bool Active(Collider c) => c != null && c.enabled && c.gameObject.activeInHierarchy;

    private void Restore()
    {
        if (itemColliders.Count == 0 && holderColliders.Count == 0) return;
        Apply(false);
        itemColliders.Clear();
        holderColliders.Clear();
    }
}
