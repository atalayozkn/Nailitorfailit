using System.Collections;
using UnityEngine;

public class ShopMarker : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private GameObject markerVisualObject;

    private Coroutine moveRoutine;
    private bool isMoving;

    public void MoveTo(Transform targetTransform)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRoutine(targetTransform));
    }
    private IEnumerator MoveRoutine(Transform targetTransform)
    {
        isMoving = true;
        markerVisualObject.SetActive(true);

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = targetTransform.position;

        float elapsedTime = 0f;

        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime;

            float t = elapsedTime / moveDuration;
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);

            yield return null;
        }

        transform.position = targetPosition;

        isMoving = false;
        markerVisualObject.SetActive(false);
        moveRoutine = null;
    }
    public bool IsMoving()
    {
        return isMoving;
    }
}