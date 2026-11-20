// Used by VolumetricFogRendererFeature. Pass 0 marches the fog into a (usually half-res) buffer:
// rgb = light scattered toward the camera, a = transmittance. Pass 1 blends that buffer over the
// camera colour with a depth-aware upsample so silhouettes stay crisp.
Shader "Hidden/NailIt/VolumetricFog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _FogDensityParams; // density, base height, height falloff, max distance
        float4 _FogShapeParams;   // sun scattering, anisotropy, noise strength, noise scale
        float4 _FogAlbedo;
        float4 _FogAmbient;
        float4 _FogWind;          // xyz = wind (m/s), w = steps
        float4 _FogLowResSize;    // w, h, 1/w, 1/h of the fog buffer
        TEXTURE3D(_FogNoise); SAMPLER(sampler_FogNoise);
        ENDHLSL

        Pass
        {
            Name "VolumetricFogMarch"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMarch
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float FogDensity(float3 p)
            {
                float height = exp(-max(0.0, p.y - _FogDensityParams.y) * _FogDensityParams.z);
                float scale = _FogShapeParams.w;
                float3 drift = _FogWind.xyz * _Time.y;
                float n1 = SAMPLE_TEXTURE3D_LOD(_FogNoise, sampler_FogNoise, (p - drift) * scale, 0).r;
                float n2 = SAMPLE_TEXTURE3D_LOD(_FogNoise, sampler_FogNoise, (p - drift * 1.7) * scale * 2.9 + 0.37, 0).r;
                float patches = smoothstep(0.22, 0.78, n1 * 0.7 + n2 * 0.3) * 2.0;
                return _FogDensityParams.x * height * lerp(1.0, patches, _FogShapeParams.z);
            }

            float4 FragMarch(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
            #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 origin = _WorldSpaceCameraPos;
                float3 toPoint = positionWS - origin;
                float dist = length(toPoint);
                float3 dir = toPoint / max(dist, 1e-4);
                float marchLength = min(dist, _FogDensityParams.w);

                int steps = (int)_FogWind.w;
                float stepLength = marchLength / steps;
                // Fixed per-pixel jitter (no TAA here, so an animated one would crawl).
                float jitter = InterleavedGradientNoise(input.positionCS.xy, 0);

                float g = _FogShapeParams.y;
                float cosTheta = dot(dir, _MainLightPosition.xyz);
                float phase = (1.0 - g * g) / pow(max(1e-3, 1.0 + g * g - 2.0 * g * cosTheta), 1.5);
                float3 sun = _MainLightColor.rgb * phase * _FogShapeParams.x;

                float3 scattered = 0;
                float transmittance = 1.0;
                [loop] for (int i = 0; i < steps; i++)
                {
                    float3 p = origin + dir * ((i + jitter) * stepLength);
                    float density = FogDensity(p);
                    if (density < 1e-5) continue;

                    float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                    float3 light = (sun * shadow + _FogAmbient.rgb) * _FogAlbedo.rgb;
                    float stepTransmittance = exp(-density * stepLength);
                    scattered += transmittance * light * (1.0 - stepTransmittance);
                    transmittance *= stepTransmittance;
                    if (transmittance < 0.01) break;
                }
                return float4(scattered, transmittance);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VolumetricFogComposite"
            // colour = scene * transmittance + scattered light
            Blend One SrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            float4 FragComposite(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 pix = uv * _FogLowResSize.xy - 0.5;
                float2 f = frac(pix);
                float2 baseUV = (floor(pix) + 0.5) * _FogLowResSize.zw;
                float d0 = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                float4 sum = 0;
                float weightSum = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 o = float2(i & 1, i >> 1);
                    float2 sampleUV = saturate(baseUV + o * _FogLowResSize.zw);
                    float d = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                    float bilinear = lerp(1.0 - f.x, f.x, o.x) * lerp(1.0 - f.y, f.y, o.y);
                    float w = bilinear / (0.02 + abs(d0 - d) / max(d0, 1e-3)) + 1e-5;
                    sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sampleUV, 0) * w;
                    weightSum += w;
                }
                return sum / weightSum;
            }
            ENDHLSL
        }
    }
}
