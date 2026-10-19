using System.Collections.Generic;
using UnityEngine;

// Dithers renderers that sit between the camera and the player instead of disabling them.
// Each obstructing renderer temporarily gets dithered copies of its materials
// (same properties, shader swapped to the matching Hidden/OcclusionDither/* shader).
public class CameraDitherHelper : MonoBehaviour
{
    private static readonly int DitherAmountId = Shader.PropertyToID("_OcclusionDitherAmount");
    private static readonly int DitherCenterId = Shader.PropertyToID("_OcclusionDitherCenter");
    private static readonly int DitherRadiusId = Shader.PropertyToID("_OcclusionDitherRadius");
    private static readonly int DitherSoftnessId = Shader.PropertyToID("_OcclusionDitherSoftness");

    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float castRadius;
    [SerializeField] private LayerMask targetLayer;

    [Header("Dither")]
    [Tooltip("Fraction of pixels removed while an object blocks the view (0 = solid, 1 = invisible).")]
    [Range(0f, 1f)][SerializeField] private float ditherAmount = 0.6f;
    [Tooltip("Radius of the see-through circle around the player, as a fraction of screen height.")]
    [Range(0f, 1f)][SerializeField] private float circleRadius = 0.15f;
    [Tooltip("Width of the circle's soft edge, as a fraction of screen height.")]
    [Range(0f, 0.5f)][SerializeField] private float circleSoftness = 0.05f;
    [Tooltip("World offset from the player pivot to the circle center (e.g. chest height).")]
    [SerializeField] private Vector3 circleCenterOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("Dithered copy of URP Lit (Hidden/OcclusionDither/Lit).")]
    [SerializeField] private Shader litDitherShader;
    [Tooltip("Dithered copy of URP Simple Lit (Hidden/OcclusionDither/SimpleLit).")]
    [SerializeField] private Shader simpleLitDitherShader;
    [Tooltip("Dithered copy of Unity Toon Shader (Hidden/OcclusionDither/Toon).")]
    [SerializeField] private Shader toonDitherShader;

    // Renderer -> its original materials (null when the renderer was disabled because nothing could be dithered).
    private readonly Dictionary<MeshRenderer, Material[]> ditheredRenderers = new();
    private readonly HashSet<MeshRenderer> currentRenderers = new();
    private readonly List<MeshRenderer> releaseBuffer = new();

    // Source material -> dithered clone, shared by every renderer using that material.
    private readonly Dictionary<Material, Material> ditherMaterials = new();
    private Dictionary<string, Shader> ditherShaders;

    private bool isActive;

    private void Awake()
    {
        ditherShaders = new Dictionary<string, Shader>
        {
            { "Universal Render Pipeline/Lit", litDitherShader },
            { "Universal Render Pipeline/Simple Lit", simpleLitDitherShader },
            { "Toon/Toon", toonDitherShader },
        };
    }

    private void OnEnable()
    {
        isActive = true;
        ApplyDitherAmount();
    }

    private void OnDisable()
    {
        isActive = false;
        ClearDitheredRenderers();
    }

    private void OnDestroy()
    {
        foreach (var material in ditherMaterials.Values)
        {
            if (material != null) Destroy(material);
        }
        ditherMaterials.Clear();
    }

    private void OnValidate()
    {
        ApplyDitherAmount();
    }

    private void Update()
    {
        if (!isActive) return;

        UpdateDitherCircle();
        CheckCameraForward();
    }

    // Tells the dither shaders where the player is on screen so only that area is dithered.
    private void UpdateDitherCircle()
    {
        Vector3 viewport = mainCamera.WorldToViewportPoint(playerTransform.position + circleCenterOffset);
        Shader.SetGlobalVector(DitherCenterId, viewport);
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
            if (!ditheredRenderers.ContainsKey(renderer))
                ditheredRenderers.Add(renderer, ApplyDither(renderer));
        }

        // No longer obstructing
        releaseBuffer.Clear();
        foreach (var renderer in ditheredRenderers.Keys)
        {
            if (!currentRenderers.Contains(renderer)) releaseBuffer.Add(renderer);
        }
        foreach (var renderer in releaseBuffer)
        {
            RestoreRenderer(renderer, ditheredRenderers[renderer]);
            ditheredRenderers.Remove(renderer);
        }
    }

    // Swaps the renderer to dithered materials and returns its originals.
    // Falls back to hiding the renderer when none of its shaders has a dither version.
    private Material[] ApplyDither(MeshRenderer renderer)
    {
        Material[] originals = renderer.sharedMaterials;
        Material[] swapped = new Material[originals.Length];
        bool anyDithered = false;

        for (int i = 0; i < originals.Length; i++)
        {
            swapped[i] = GetDitherMaterial(originals[i]);
            if (swapped[i] != originals[i]) anyDithered = true;
        }

        if (!anyDithered)
        {
            renderer.enabled = false;
            return null;
        }

        renderer.sharedMaterials = swapped;
        return originals;
    }

    private void RestoreRenderer(MeshRenderer renderer, Material[] originals)
    {
        if (renderer == null) return;

        if (originals == null) renderer.enabled = true;
        else renderer.sharedMaterials = originals;
    }

    private Material GetDitherMaterial(Material source)
    {
        if (source == null) return null;
        if (ditherMaterials.TryGetValue(source, out var cached)) return cached;

        if (!ditherShaders.TryGetValue(source.shader.name, out var ditherShader) || ditherShader == null)
            return source;

        var clone = new Material(source)
        {
            name = source.name + " (Dither)",
            shader = ditherShader,
            renderQueue = source.renderQueue,
            hideFlags = HideFlags.DontSave
        };
        clone.shaderKeywords = source.shaderKeywords;

        ditherMaterials.Add(source, clone);
        return clone;
    }

    private void ApplyDitherAmount()
    {
        Shader.SetGlobalFloat(DitherAmountId, ditherAmount);
        Shader.SetGlobalFloat(DitherRadiusId, circleRadius);
        Shader.SetGlobalFloat(DitherSoftnessId, circleSoftness);
    }

    private void ClearDitheredRenderers()
    {
        foreach (var pair in ditheredRenderers)
            RestoreRenderer(pair.Key, pair.Value);

        ditheredRenderers.Clear();
        currentRenderers.Clear();
    }

    public void SetActivity(bool condition)
    {
        if (isActive == condition) return;

        isActive = condition;

        if (!isActive)
            ClearDitheredRenderers();
    }
}
