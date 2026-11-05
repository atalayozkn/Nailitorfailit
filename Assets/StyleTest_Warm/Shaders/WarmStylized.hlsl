#ifndef WARM_STYLIZED_INCLUDED
#define WARM_STYLIZED_INCLUDED

// One-light stylized surface for the warm art style test.
// Cost: base map + one noise sample + main light shadow. No PBR, no reflections, no additional lights.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

#ifdef _WARM_DITHER
#include "Assets/Shaders/OcclusionDither/OcclusionDither.hlsl"
#define WARM_DITHER_CLIP(positionCS) OcclusionDitherClip(positionCS)
#else
#define WARM_DITHER_CLIP(positionCS)
#endif

TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);
TEXTURE2D(_PaintNoise);   SAMPLER(sampler_PaintNoise);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half4 _ShadowTint;
    half4 _RimColor;
    half _RampOffset;
    half _RampSoft;
    half _PaintStrength;
    float _PaintScale;
    half _RimPower;
    half _Cutoff;
    half _WindAmount;
    half _WindSpeed;
CBUFFER_END

// Set by GlobalWind, same globals the existing Wind Sway shaders read.
float4 _NailWindDirection;
float _NailWindSpeed;
float _NailWindStrength;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    float2 wind       : TEXCOORD1; // x = sway weight (0 at the base, 1 at the tips)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Bases stay planted, tips move. Phase comes from the plant origin plus a little per vertex,
// so a canopy moves together but its clumps still flutter.
float3 WarmWind(float3 positionOS, float weight)
{
#ifdef _WIND
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
    float phase = dot(originWS.xz, float2(0.37, 0.61)) + dot(positionWS.xz, float2(0.11, 0.07));
    float t = _Time.y * _NailWindSpeed * _WindSpeed;
    float wave = sin(t + phase) + 0.5 * sin(t * 2.3 + phase * 1.7);
    float flutter = sin(t * 5.3 + dot(positionWS, float3(3.1, 2.3, 4.7))) * 0.3;
    float amount = weight * _WindAmount * _NailWindStrength;
    positionWS.xz += _NailWindDirection.xz * (wave + flutter) * amount;
    positionWS.y += flutter * amount * 0.3;
    return TransformWorldToObject(positionWS);
#else
    return positionOS;
#endif
}

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS    : TEXCOORD2;
    half fogFactor    : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings WarmVert(Attributes input)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS = TransformObjectToWorld(WarmWind(input.positionOS.xyz, input.wind.x));
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    o.fogFactor = ComputeFogFactor(o.positionCS.z);
    return o;
}

half4 WarmFrag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    WARM_DITHER_CLIP(input.positionCS);

    half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
#ifdef _ALPHATEST_ON
    clip(base.a - _Cutoff);
#endif
    half3 albedo = base.rgb;

    // World-space brush mottling: hides photo detail and flat swatches with one sample.
    float2 paintUV = (input.positionWS.xz + input.positionWS.yy * float2(0.37, 0.61)) * _PaintScale;
    half2 paint = SAMPLE_TEXTURE2D(_PaintNoise, sampler_PaintNoise, paintUV).rg;
    albedo *= lerp(1.0, 0.8 + 0.4 * paint.r, _PaintStrength);

#if defined(_DBUFFER)
    ApplyDecalToBaseColor(input.positionCS, albedo);
#endif

    half3 normalWS = normalize(input.normalWS);
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1, 1, 1, 1));

    half ndl = dot(normalWS, mainLight.direction) + (paint.g - 0.5) * 0.2 * _PaintStrength;
    half band = smoothstep(_RampOffset - _RampSoft, _RampOffset + _RampSoft, ndl);
    half lit = band * mainLight.shadowAttenuation;

    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
    half3 ambient = SampleSH(normalWS) * ao.indirectAmbientOcclusion;

    // Shadows get a coloured fill instead of going grey.
    half3 shadowed = albedo * (ambient + _ShadowTint.rgb);
    half3 sunlit = albedo * (ambient + mainLight.color);
    half3 color = lerp(shadowed, sunlit, lit * ao.directAmbientOcclusion);

    half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half rim = pow(saturate(1.0 - dot(normalWS, viewDir)), _RimPower);
    color += _RimColor.rgb * rim * (0.35 + 0.65 * lit);

#ifdef _EMISSION
    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
#endif

    color = MixFog(color, input.fogFactor);
    return half4(color, 1.0);
}

// Depth, depth-normals and shadow passes share this minimal setup.
struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    half3 normalWS    : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

DepthVaryings DepthVert(Attributes input)
{
    DepthVaryings o = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS = TransformObjectToHClip(WarmWind(input.positionOS.xyz, input.wind.x));
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return o;
}

void WarmAlphaClip(float2 uv)
{
#ifdef _ALPHATEST_ON
    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
#endif
}

half DepthFrag(DepthVaryings input) : SV_Target
{
    WARM_DITHER_CLIP(input.positionCS);
    WarmAlphaClip(input.uv);
    return input.positionCS.z;
}

half4 DepthNormalsFrag(DepthVaryings input) : SV_Target
{
    WARM_DITHER_CLIP(input.positionCS);
    WarmAlphaClip(input.uv);
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
}

float3 _LightDirection;

DepthVaryings ShadowVert(Attributes input)
{
    DepthVaryings o = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    float3 positionWS = TransformObjectToWorld(WarmWind(input.positionOS.xyz, input.wind.x));
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
    o.positionCS = ApplyShadowClamping(positionCS);
    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return o;
}

half ShadowFrag(DepthVaryings input) : SV_Target
{
    WarmAlphaClip(input.uv);
    return 0;
}

#endif
