using UnityEngine;
using UnityEngine.Events;

public class Pipe : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform muzzle;

    [Header("Settings")]
    [SerializeField] private Vector3 halfExtends;
    [SerializeField] private LayerMask targetLayer;

    [Range(0f, 1f)]
    [SerializeField] private float effectPercent;

    [Header("Events")]
    [SerializeField] private UnityEvent onTriggerEvent;
    [SerializeField] private UnityEvent onHazardEndEvent;

    public void OnTrigger()
    {
        onTriggerEvent?.Invoke();

        Collider[] colliders = Physics.OverlapBox(muzzle.transform.position, halfExtends, muzzle.transform.rotation, targetLayer, QueryTriggerInteraction.Ignore);
        if (colliders.Length == 0) return;

        foreach (var collider in colliders)
        {
            if (Random.value > effectPercent) //Return value between 0 and 1 compare and continue.
                continue;

            if (collider.gameObject.TryGetComponent<Wood>(out Wood wood))
            {
                wood.TriggerPuddle();
            }
        }

        Invoke(nameof(StopEffects), 5.0f);
    }

    private void StopEffects()
    {
        onHazardEndEvent?.Invoke();
    }
    private void OnDrawGizmosSelected()
    {
        if (muzzle == null) return;

        Gizmos.color = Color.cyan;
        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.TRS(
            muzzle.position,
            muzzle.rotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(Vector3.zero, halfExtends * 2f);

        Gizmos.matrix = oldMatrix;
    }
}
