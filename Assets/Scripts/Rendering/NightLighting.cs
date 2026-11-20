using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Put one in a level that takes place at night (Level_04). While it is enabled:
//  - the warm stylized shader takes point and spot lights (street lamps, fires), which it ignores elsewhere,
//  - burning tiles get a flickering fire light (VfxManager asks AttachFireLight),
//  - the night ambient is re-applied, because the levels share one lighting data asset whose
//    ambient probe was baked in daylight.
// The sun/moon, sky, fog, lamp lights and post volume are ordinary scene objects and settings.
[ExecuteAlways]
[DisallowMultipleComponent]
public class NightLighting : MonoBehaviour
{
    private static readonly int AdditionalLightsId = Shader.PropertyToID("_NailAdditionalLights");

    public static NightLighting Current { get; private set; }

    [Header("Ambient")]
    [SerializeField] private Color ambientSky = new(0.16f, 0.2f, 0.36f);
    [SerializeField] private Color ambientEquator = new(0.13f, 0.14f, 0.24f);
    [SerializeField] private Color ambientGround = new(0.08f, 0.08f, 0.12f);

    [Header("Fire light")]
    [SerializeField] private Color fireColor = new(1f, 0.55f, 0.22f);
    [SerializeField] private float fireIntensity = 5f;
    [SerializeField] private float fireRange = 5f;
    [Tooltip("Height of the light above the burning tile.")]
    [SerializeField] private float fireHeight = 0.6f;
    [Tooltip("Ignition flashes this many times brighter, then fades.")]
    [SerializeField] private float igniteFlash = 2f;

    private void OnEnable()
    {
        Current = this;
        Shader.SetGlobalFloat(AdditionalLightsId, 1f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
    }

    private void OnDisable()
    {
        if (Current != this) return;
        Current = null;
        Shader.SetGlobalFloat(AdditionalLightsId, 0f);
    }

    // Adds a light that lives and dies with the effect. ignite = short flash instead of a steady flicker.
    public void AttachFireLight(GameObject effect, bool ignite)
    {
        if (effect == null) return;
        var host = new GameObject("FireLight");
        host.transform.SetParent(effect.transform, false);
        host.transform.position = effect.transform.position + Vector3.up * fireHeight;

        var lightSource = host.AddComponent<Light>();
        lightSource.type = LightType.Point;
        lightSource.color = fireColor;
        lightSource.range = fireRange;
        lightSource.intensity = fireIntensity;
        lightSource.shadows = LightShadows.None;
        lightSource.renderMode = LightRenderMode.ForcePixel;
        AllRenderingLayers(lightSource);

        var flicker = host.AddComponent<FireLightFlicker>();
        flicker.Init(fireIntensity, ignite ? igniteFlash : 0f);
    }

    // Rendering layers are on, and the terrain sits on layer 2 while new lights only cover Default,
    // so without this the ground would not take the light (same fix the warm style applies to the sun).
    public static void AllRenderingLayers(Light lightSource)
    {
        lightSource.renderingLayerMask = -1;
        var data = lightSource.GetUniversalAdditionalLightData();
        if (data != null) data.renderingLayers = (RenderingLayerMask)uint.MaxValue;
    }
}
