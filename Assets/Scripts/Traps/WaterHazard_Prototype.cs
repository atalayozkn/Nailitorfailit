using UnityEngine;
using UnityEngine.Events;
using Wettables;

public class WaterHazard_Prototype : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private Vector3 halfDimensions;

    [Header("Events")]
    [SerializeField] private UnityEvent onTriggerEvent;

    [Header("Debug")]
    [SerializeField] private bool isDebugMode;

    public void OnTrigger()
    {
        onTriggerEvent?.Invoke();

        Collider[] colliders = Physics.OverlapBox(muzzleTransform.position, halfDimensions, muzzleTransform.rotation, targetLayer);
        if (colliders.Length == 0) return;

        foreach (var col in colliders)
        {
            if (col.gameObject.TryGetComponent<IWettable>(out var wettable))
            {
                wettable.OnWaterContact();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!isDebugMode || muzzleTransform == null) return;
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(muzzleTransform.position, muzzleTransform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfDimensions * 2f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}