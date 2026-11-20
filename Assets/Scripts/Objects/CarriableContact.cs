using System.Collections.Generic;
using UnityEngine;

// Lets carriable items bump into each other (stack, knock each other over, block a throw).
// Carriables live on the Interaction layer, which does not collide with itself on purpose: unfinished tiles,
// blueprints and stations share that layer. So instead of changing that, every item gets a hidden copy of its solid
// colliders on its own layer (CarriableContact) that collides only with itself. The copies sit on the same rigidbody,
// so item-vs-item contacts move the items like normal collisions, and nothing else (player, interaction casts,
// triggers, stations) ever sees them.
// The copies follow their source collider: off while the item is carried, stowed, consumed or held by the dog.
public class CarriableContact : MonoBehaviour
{
    // Also named in Tags and Layers. The matrix is set here at startup as well, so it holds even if that name is lost.
    public const int Layer = 12;
    const string CopyName = "__CarriableContact";

    private readonly List<(Collider source, Collider copy)> pairs = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetupLayerMatrix()
    {
        for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(Layer, i, i != Layer);
    }

    // Items that start out overlapping (spawned or dropped on one spot) separate gently instead of popping apart
    // at the project's 10 m/s depenetration limit.
    const float MaxDepenetrationSpeed = 2f;

    private void Awake()
    {
        if (TryGetComponent(out Rigidbody rb))
            rb.maxDepenetrationVelocity = Mathf.Min(rb.maxDepenetrationVelocity, MaxDepenetrationSpeed);

        var sources = new List<Collider>();
        GetComponentsInChildren(true, sources);
        foreach (Collider source in sources)
        {
            if (source == null || source.isTrigger || source.name.StartsWith("__")) continue;
            Collider copy = CreateCopy(source);
            if (copy != null) pairs.Add((source, copy));
        }
        Sync();
    }

    private void FixedUpdate() => Sync();

    // Match every copy to its source now (called after the item switches its colliders on or off).
    public void Sync()
    {
        foreach (var (source, copy) in pairs)
        {
            if (copy == null) continue;
            bool on = source != null && source.enabled && source.gameObject.activeInHierarchy && !source.isTrigger;
            if (copy.enabled != on) copy.enabled = on;
        }
    }

    private static Collider CreateCopy(Collider source)
    {
        var go = new GameObject(CopyName) { layer = Layer };
        go.transform.SetParent(source.transform, false);

        Collider copy;
        switch (source)
        {
            case BoxCollider b:
                var box = go.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
                copy = box;
                break;
            case SphereCollider s:
                var sphere = go.AddComponent<SphereCollider>();
                sphere.center = s.center;
                sphere.radius = s.radius;
                copy = sphere;
                break;
            case CapsuleCollider c:
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.center = c.center;
                capsule.radius = c.radius;
                capsule.height = c.height;
                capsule.direction = c.direction;
                copy = capsule;
                break;
            case MeshCollider m when m.sharedMesh != null:
                var mesh = go.AddComponent<MeshCollider>();
                mesh.sharedMesh = m.sharedMesh;
                mesh.convex = true; // the item is a dynamic body
                copy = mesh;
                break;
            default:
                Destroy(go);
                return null;
        }
        copy.sharedMaterial = source.sharedMaterial;
        copy.contactOffset = source.contactOffset;
        copy.enabled = false;
        return copy;
    }
}
