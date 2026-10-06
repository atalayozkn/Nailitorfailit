using System.Collections.Generic;
using UnityEngine;

public class CameraDitherHelper : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float castRadius;
    [SerializeField] private LayerMask targetLayer;

    private readonly HashSet<MeshRenderer> hiddenRenderers = new();
    private readonly HashSet<MeshRenderer> currentRenderers = new();

    private bool isActive;

    private void OnEnable()
    {
        isActive = true;
    }

    private void OnDisable()
    {
        isActive = false;
        ClearHiddenRenderers();
    }

    private void Update()
    {
        if (!isActive) return;

        CheckCameraForward();
    }

    private void CheckCameraForward()
    {
        currentRenderers.Clear();

        Vector3 point1 = mainCamera.transform.position;
        Vector3 point2 = playerTransform.position;

        Collider[] cols = Physics.OverlapCapsule(
            point1,
            point2,
            castRadius,
            targetLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (var col in cols)
        {
            if (!col.TryGetComponent<MeshRenderer>(out var renderer))
                continue;

            currentRenderers.Add(renderer);

            // Newly obstructing renderer
            if (hiddenRenderers.Add(renderer))
            {
                renderer.enabled = false;
            }
        }

        // No longer obstructing
        foreach (var renderer in hiddenRenderers)
        {
            if (!currentRenderers.Contains(renderer))
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
        }

        // Keep only currently obstructing renderers
        hiddenRenderers.IntersectWith(currentRenderers);
    }

    private void ClearHiddenRenderers()
    {
        foreach (var renderer in hiddenRenderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }

        hiddenRenderers.Clear();
        currentRenderers.Clear();
    }

    public void SetActivity(bool condition)
    {
        if (isActive == condition) return;

        isActive = condition;

        if (!isActive)
            ClearHiddenRenderers();
    }
}