using Unity.Cinemachine;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private CinemachineCamera activeCamera;

    private void LateUpdate()
    {
        if (activeCamera == null) return;

        Vector3 direction = activeCamera.transform.position - transform.position;

        if (direction.sqrMagnitude <= 0.1f) return;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}