using System.Collections;
using UnityEngine;

public class ShopMarker : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private GameObject markerVisualObject;

    private bool isMoving = false;

    private void OnEnable()
    {
        markerVisualObject.SetActive(false);
        isMoving = false;
    }
    public void MoveTo(Transform targetTransform)
    {
        if (isMoving) return;
        isMoving = true;
        StartCoroutine(MoveRoutine(targetTransform));
    }
    private IEnumerator MoveRoutine(Transform targetTransform)
    {
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
    }
    public void SetVisual(bool condition)
    {
        markerVisualObject.SetActive(condition);
    }
}