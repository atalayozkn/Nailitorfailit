using Unity.Cinemachine;
using UnityEngine;

public class PlayerCameraFOVHelper : MonoBehaviour
{
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private float fovChangeSpeed = 5f;

    private float targetFOV;
    private float currentFOV;
    private bool isActive;

    private void Awake()
    {
        currentFOV = playerCamera.Lens.FieldOfView;
        targetFOV = currentFOV;
    }

    private void Update()
    {
        if (!isActive) return;

        currentFOV = Mathf.MoveTowards(currentFOV, targetFOV, fovChangeSpeed * Time.deltaTime);
        LensSettings lens = playerCamera.Lens;
        lens.FieldOfView = currentFOV;
        playerCamera.Lens = lens;

        if (Mathf.Approximately(currentFOV, targetFOV))
        {
            currentFOV = targetFOV;
            isActive = false;
        }
    }

    public void SetTargetFOV(float amount)
    {
        targetFOV = amount;

        if (!Mathf.Approximately(targetFOV, currentFOV))
        {
            isActive = true;
        }
    }
}