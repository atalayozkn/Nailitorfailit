using UnityEngine;

public class DogPatrolArea_MP : MonoBehaviour
{
    [SerializeField] private DogStateMachine dog;

    [Tooltip("Optional: exact zone id. Empty = nearest DogPatrol zone to this object.")]
    [SerializeField] private string zoneId = "";

    [Tooltip("Extra margin (m) inward so the dog does not hug the very edge.")]
    [SerializeField] private float margin = 0f;

    private Collider[] appliedBoundaries;

    private void Awake()
    {
        if (dog == null) dog = GetComponentInChildren<DogStateMachine>(true);
    }

    private void OnEnable()
    {
        Apply();
        if (appliedBoundaries == null) StartCoroutine(ApplyNextFrame());
    }

    private System.Collections.IEnumerator ApplyNextFrame()
    {
        yield return null;
        Apply();
    }

    private void Apply()
    {
        if (dog == null) return;

        ZoneVolume zone = string.IsNullOrWhiteSpace(zoneId)
            ? ZoneRegistry.Nearest(transform.position, ZoneKind.DogPatrol)
            : ZoneRegistry.Find(zoneId, ZoneKind.DogPatrol);

        if (zone == null)
        {
            return;
        }

        BoxCollider box = zone.Box;
        if (box == null) return;

        if (!ApplyBoundaries(new Collider[] { box })) return;
        appliedBoundaries = new Collider[] { box };

        Debug.Log($"[ZONE] Dog patrol area '{zone.ZoneId}' applied to '{dog.name}' " +
                  $"(box {box.size}, center {zone.Center}).");
    }

    private static readonly System.Reflection.MethodInfo PatrolBoundariesSetter =
        typeof(DogStateMachine)
            .GetProperty("patrolBoundaries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.GetSetMethod(true);

    private bool ApplyBoundaries(Collider[] boundaries)
    {
        if (PatrolBoundariesSetter == null)
        {
            Debug.LogWarning("[ZONE] DogStateMachine.patrolBoundaries is read-only; keeping prefab boundaries.");
            return false;
        }

        PatrolBoundariesSetter.Invoke(dog, new object[] { boundaries });
        return true;
    }

    private void OnDisable()
    {
        appliedBoundaries = null;
    }
}
