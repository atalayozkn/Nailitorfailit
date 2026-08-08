using UnityEngine;

public class PlayerCameraFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;

    [Header("Settings")]
    [SerializeField] private bool followX = true;
    [SerializeField] private float xTrackFrequency = 1f;

    [SerializeField] private bool followY = true;
    [SerializeField] private float yTrackFrequency = 1f;

    [SerializeField] private bool followZ = false;
    [SerializeField] private float zTrackFrequency = 1f;

    private Vector3 previousTargetPosition;

    private void Awake()
    {
        if (followTarget == null) return;

        transform.position = followTarget.position;
        previousTargetPosition = followTarget.position;
    }

    private void LateUpdate()
    {
        if (followTarget == null) return;

        Vector3 targetDelta = followTarget.position - previousTargetPosition;
        Vector3 position = transform.position;

        if (followX) position.x += targetDelta.x * xTrackFrequency;

        if (followY) position.y += targetDelta.y * yTrackFrequency;

        if (followZ) position.z += targetDelta.z * zTrackFrequency;

        transform.position = position;

        previousTargetPosition = followTarget.position;
    }
}