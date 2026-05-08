using System.Collections;
using UnityEngine;

public class TestPressureApplier : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private LayerMask whatIsFragile;
    [SerializeField] private float rayDistance = 2f;
    [SerializeField] private float checkInterval = 1f;

    private bool isHeavy = false;

    private Coroutine pressureCoroutine;

    private void OnEnable()
    {
        OnPickUp();
    }
    public void OnPickUp()
    {
        isHeavy = true;
        if (pressureCoroutine != null)
        {
            StopCoroutine(pressureCoroutine);
        }
        pressureCoroutine = StartCoroutine(PressureApplyCheck());
    }

    public void OnDrop()
    {
        isHeavy = false;

        if (pressureCoroutine != null)
        {
            StopCoroutine(pressureCoroutine);
            pressureCoroutine = null;
        }
    }
    private IEnumerator PressureApplyCheck()
    {
        while (isHeavy)
        {
            Ray ray = new Ray(transform.position, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, whatIsFragile, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.TryGetComponent(out Glass fragileObj))
                {
                    fragileObj.ApplyPressure();
                }
            }
            yield return new WaitForSeconds(checkInterval);
        }
        pressureCoroutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position,transform.position + Vector3.down * rayDistance);
    }
}