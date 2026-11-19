// Stylized see-through glass for carriable glass and the glass cabinet (URP).
// Thin tinted body, edges that thicken and tint at grazing angles, a hard-edged toon specular, a soft sky
// reflection and two diagonal glint streaks fixed to the object. Premultiplied blending keeps the shine bright
// on top of the faint body. Hover highlight is unaffected: NailIt/HoverGlow draws on its own mesh copy in a
// later queue (Transparent+10), so it always lands on top of this material.
Shader "NailIt/StylizedGlass"
{
    Properties
    {
        [MainColor] _BaseColor("Tint (alpha = body opacity)", Color) = (0.62, 0.9, 0.95, 0.16)
        _EdgeColor("Edge Color (alpha = edge opacity)", Color) = (0.72, 0.96, 1, 0.8)
        _FresnelPower("Edge Falloff", Range(0.5, 8)) = 2.5
        _Frost("Frost", Range(0, 1)) = 0

        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize("Specular Size", Range(0, 1)) = 0.14
        _SpecularSoftness("Specular Softness", Range(0.001, 1)) = 0.2

        _StreakColor("Glint Streak Color (alpha = strength)", Color) = (1, 1, 1, 0.5)
        _StreakScale("Glint Streak Scale", Float) = 1.6
        _StreakWidth("Glint Streak Width", Range(0, 0.5)) = 0.1
        _StreakTilt("Glint Streak Tilt", Range(-2, 2)) = 0.8
        _StreakDrift("Glint Streak Drift Speed", Float) = 0.08

        _Reflection("Sky Reflection", Range(0, 1)) = 0.35
        _ShadowDensity("Shadow Density", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EdgeColor;
            half _FresnelPower;
            half _Frost;
            half4 _SpecularColor;
            half _SpecularSize;
            half _SpecularSoftness;
            half4 _StreakColor;
            float _StreakScale;
            half _StreakWidth;
            float _StreakTilt;
            float _StreakDrift;
            half _Reflection;
            half _ShadowDensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half fogCoord : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // 0..1 band centred on the middle of each repeat, soft by a fixed amount so it never aliases.
            half Band(float coord, half width)
            {
                float d = abs(frac(coord) - 0.5);
                return 1.0 - smoothstep(width, width + 0.02, d);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight();

                half nv = saturate(dot(n, v));
                half fresnel = pow(1.0 - nv, _FresnelPower);

                // Body: tint lit softly so the dark side is still glassy, plus ambient.
                half wrap = saturate(dot(n, light.direction) * 0.5 + 0.5);
                half3 lit = _BaseColor.rgb * (light.color * lerp(0.55, 1.0, wrap) + SampleSH(n) * 0.5);

                // Sky reflection, stronger toward the edges. Frost blurs it.
                half3 env = GlossyEnvironmentReflection(reflect(-v, n), 0.1 + _Frost * 0.6, 1.0);
                lit = lerp(lit, env, _Reflection * (0.3 + 0.7 * fresnel));

                // Edges look thicker and more coloured.
                lit = lerp(lit, _EdgeColor.rgb, fresnel * _EdgeColor.a);
                half bodyAlpha = saturate(_BaseColor.a + fresnel * _EdgeColor.a + _Frost * 0.45);
                lit = lerp(lit, lit * 0.6 + 0.4, _Frost * 0.5);

                // Hard toon specular from the main light.
                half nh = saturate(dot(n, normalize(light.direction + v)));
                half threshold = 1.0 - _SpecularSize * _SpecularSize;
                half softness = _SpecularSoftness * (1.0 - threshold) + 0.0005;
                half spec = smoothstep(threshold - softness, threshold + softness, nh) * (1.0 - _Frost * 0.7);

                // Two diagonal glint streaks fixed to the object, drifting slowly.
                float s = (i.positionOS.x + i.positionOS.y * _StreakTilt + i.positionOS.z * 0.5) * _StreakScale + _Time.y * _StreakDrift;
                half streak = max(Band(s, _StreakWidth), Band(s + 0.18, _StreakWidth * 0.35) * 0.7);
                streak *= _StreakColor.a * (1.0 - _Frost * 0.6) * (0.4 + 0.6 * nv);

                half3 shine = _SpecularColor.rgb * light.color * spec + _StreakColor.rgb * streak;
                half3 color = lit * bodyAlpha + shine;
                half alpha = saturate(bodyAlpha + spec + streak * 0.5);

                color = MixFog(color, i.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        // Light dithered shadow so glass still sits on the ground without a solid dark block.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                float noise = frac(52.9829189 * frac(dot(positionCS.xy, float2(0.06711056, 0.00583715))));
                clip(_ShadowDensity - noise - 0.001);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
