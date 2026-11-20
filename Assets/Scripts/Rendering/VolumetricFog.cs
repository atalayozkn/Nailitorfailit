using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Raymarched height fog lit by the sun (with its shadows, so buildings cut light shafts) plus a flat ambient term.
// Drawn by VolumetricFogRendererFeature on PC_Renderer. It does nothing until a Volume turns it on, so only
// scenes whose own volume profile carries it (Level_02's smog) get fog.
[Serializable]
[VolumeComponentMenu("NailIt/Volumetric Fog")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class VolumetricFog : VolumeComponent
{
    [Header("Density")]
    [Tooltip("Extinction per metre at and below Base Height. 0 turns the effect off.")]
    public ClampedFloatParameter density = new(0f, 0f, 0.3f);
    [Tooltip("World height where the fog is thickest. Above it, density fades out.")]
    public FloatParameter baseHeight = new(0f);
    [Tooltip("How fast density fades above Base Height (per metre).")]
    public ClampedFloatParameter heightFalloff = new(0.15f, 0f, 2f);
    [Tooltip("Fog is only marched this far from the camera; the sky gets this much fog too.")]
    public ClampedFloatParameter maxDistance = new(80f, 5f, 400f);

    [Header("Look")]
    [Tooltip("Colour the fog tints the light it scatters.")]
    public ColorParameter albedo = new(new Color(0.85f, 0.82f, 0.6f), false, false, true);
    [Tooltip("Light the fog has even in shade, so it never goes black.")]
    public ColorParameter ambient = new(new Color(0.3f, 0.3f, 0.22f), true, false, true);
    [Tooltip("How much sunlight the fog scatters toward the camera.")]
    public ClampedFloatParameter sunScattering = new(1f, 0f, 8f);
    [Tooltip("Forward scattering: higher values brighten the fog looking toward the sun.")]
    public ClampedFloatParameter anisotropy = new(0.4f, -0.9f, 0.9f);

    [Header("Motion")]
    [Tooltip("0 = uniform fog, 1 = fully patchy.")]
    public ClampedFloatParameter noiseStrength = new(0.6f, 0f, 1f);
    [Tooltip("Size of the fog patches (higher = smaller patches).")]
    public ClampedFloatParameter noiseScale = new(0.04f, 0.002f, 0.5f);
    [Tooltip("Drift of the fog patches in metres per second.")]
    public Vector3Parameter wind = new(new Vector3(0.6f, 0.05f, 0.3f));

    [Header("Quality")]
    public ClampedIntParameter steps = new(32, 8, 96);

    public bool IsActive() => active && density.value > 0f;
}
