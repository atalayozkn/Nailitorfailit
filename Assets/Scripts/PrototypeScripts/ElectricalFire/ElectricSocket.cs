using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ElectricSocket : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform muzzleTransform;

    [Header("Fire Settings")]
    [SerializeField] private Vector3 effectAreaHalfDimensions;
    [SerializeField] private LayerMask whatIsWood;

    [Header("Events")]
    [SerializeField] private UnityEvent onActionStartEvent;
    [SerializeField] private UnityEvent onActionStopEvent;
    [SerializeField] private UnityEvent onHitTryEvent;
    private bool isActive = false;
    public void OnActivate()
    {
        if (isActive) return;
        isActive = true;
        onActionStartEvent?.Invoke();
        StartCoroutine(FireRoutine());
    }
    private IEnumerator FireRoutine()
    {
        int Counter = 0;

        while (isActive && Counter < 10)
        {
            Counter++;
            TryHit();
            onHitTryEvent?.Invoke();
            yield return new WaitForSeconds(1.0f);
        }

        onActionStopEvent?.Invoke();
        isActive = false;
    }
    private void TryHit()
    {
        //Make a OverlapBox Check
        Collider[] colliders = Physics.OverlapBox(muzzleTransform.position, effectAreaHalfDimensions, muzzleTransform.rotation, whatIsWood, QueryTriggerInteraction.Ignore);

        if (colliders.Length != 0)
        {
            foreach (var col in colliders)
            {
                Wood woodObj = col.gameObject.GetComponent<Wood>();
                woodObj?.OnFireHit();
            }
        }
    }
    //Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.TRS(muzzleTransform.position,muzzleTransform.rotation,Vector3.one);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, effectAreaHalfDimensions * 2f);
    }
}
