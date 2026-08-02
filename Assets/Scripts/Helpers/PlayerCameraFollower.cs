using UnityEngine;

public class PlayerCameraFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;

    [Header("Settings")]
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;
    [SerializeField] private bool followZ = false;

    private Vector3 position;

    private void Awake()
    {
        transform.position = followTarget.position;
    }
    private void LateUpdate()
    {
        if (followTarget == null)
            return;

        position = transform.position;

        if (followX)
            position.x = followTarget.position.x;

        if (followY)
            position.y = followTarget.position.y;

        if (followZ)
            position.z = followTarget.position.z;

        transform.position = position;
    }
}