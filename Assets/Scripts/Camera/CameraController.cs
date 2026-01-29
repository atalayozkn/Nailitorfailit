using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _cinemachineCam;

    public void OnPlayerInitialized(Transform target)
    {
        if (_cinemachineCam != null)
        {
            _cinemachineCam.Follow = target;
            _cinemachineCam.LookAt = target;
        }
    }
}
