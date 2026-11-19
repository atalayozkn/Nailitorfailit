// Floor glass tile on the shared floor tile mesh (zemin yeni). Opaque "fake" glass so it stays readable as a floor
// and the wet/crack overlays, shadows and SSAO keep working: a cool tinted body with fake thickness that shifts with
// the view, bright bevelled panel edges, a hard sun highlight, diagonal glints and a sky reflection at grazing angles.
// Everything is measured from the tile centre, so it needs no UVs and works whatever atlas the mesh was made for.
Shader "NailIt/GlassTile"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (only used by the blueprint hologram)", 2D) = "white" {}
        [MainColor] _BaseColor("Glass Tint", Color) = (0.62, 0.86, 0.92, 1)
        _DeepColor("Deep Colour", Color) = (0.16, 0.36, 0.46, 1)
        _Depth("Fake Thickness", Range(0, 0.3)) = 0.12
        _EdgeColor("Bevel Edge Colour", Color) = (0.9, 0.98, 1, 1)
        _FrameWidth("Bevel Width (m)", Range(0.005, 0.15)) = 0.05
        _TileHalfSize("Tile Half Size (m)", Float) = 0.5
        _SpecularSize("Sun Highlight Size", Range(0, 1)) = 0.18
        _GlintColor("Glint Colour (alpha = strength)", Color) = (1, 1, 1, 0.45)
        _GlintScale("Glint Spacing", Float) = 1.3
        _GlintWidth("Glint Width", Range(0, 0.5)) = 0.07
        _Reflection("Sky Reflection", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _DeepColor;
            float _Depth;
            half4 _EdgeColor;
            float _FrameWidth;
            float _TileHalfSize;
            half _SpecularSize;
            half4 _GlintColor;
            float _GlintScale;
            half _GlintWidth;
            half _Reflection;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogCoord : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // 0 at the tile border, grows towards the middle (metres).
            float InsetDistance(float2 local)
            {
                return min(_TileHalfSize - abs(local.x), _TileHalfSize - abs(local.y));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float2 local = i.positionWS.xz - TransformObjectToWorld(float3(0, 0, 0)).xz;
                float top = saturate(n.y * 2.0 - 0.5);                       // 1 on the walking surface, 0 on the sides

                // Fake thickness: look "into" the slab along the view ray; the inner panel darkens towards its middle.
                float2 deepLocal = local - v.xz / max(v.y, 0.25) * _Depth;
                float inner = smoothstep(0.0, _TileHalfSize * 0.8, InsetDistance(deepLocal));
                half3 body = lerp(_BaseColor.rgb, _DeepColor.rgb, inner * 0.55 * top);

                // Lighting: soft wrap so glass never goes flat black, receives shadows and ambient.
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = light.shadowAttenuation;
                half wrap = saturate(dot(n, light.direction) * 0.5 + 0.5);
                half3 lit = body * (light.color * lerp(0.55, 1.0, wrap * shadow) + SampleSH(n) * 0.6);

                // Bright bevel along the panel edge (and on the slab sides).
                float frame = 1.0 - smoothstep(_FrameWidth * 0.4, _FrameWidth, InsetDistance(local));
                lit = lerp(lit, _EdgeColor.rgb * (0.75 + 0.25 * shadow), max(frame * top * 0.7, (1.0 - top) * 0.5));

                // Sky reflection, stronger at grazing angles.
                half fresnel = pow(1.0 - saturate(dot(n, v)), 4.0);
                half3 sky = GlossyEnvironmentReflection(reflect(-v, n), 0.05, 1.0);
                lit = lerp(lit, sky, saturate(_Reflection * (0.15 + 0.85 * fresnel)));

                // Hard toon sun highlight and two diagonal glints across the panel.
                half nh = saturate(dot(n, normalize(light.direction + v)));
                half threshold = 1.0 - _SpecularSize * _SpecularSize;
                half spec = smoothstep(threshold - 0.01, threshold + 0.01, nh) * shadow;
                float s = (local.x + local.y) * _GlintScale;
                float glint = max(1.0 - smoothstep(_GlintWidth, _GlintWidth + 0.02, abs(frac(s) - 0.5)),
                                  (1.0 - smoothstep(_GlintWidth * 0.35, _GlintWidth * 0.35 + 0.02, abs(frac(s + 0.2) - 0.5))) * 0.6);
                glint *= _GlintColor.a * top * (1.0 - frame);
                lit += light.color * spec + _GlintColor.rgb * glint;

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    lit *= ao.directAmbientOcclusion;
                #endif

                return half4(MixFog(lit, i.fogCoord), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

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
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct Attributes { float4 positionOS : POSITION; };
            float4 DepthVert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings NormalsVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 NormalsFrag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
