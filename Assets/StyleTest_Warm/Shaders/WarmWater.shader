// Stylized pond water: depth-tinted colour, soft foam at the shore, drifting sparkles.
// One transparent pass, two noise reads. Depth is painted from the mesh UV (0 centre, 1 shore),
// because the level terrain is already at its minimum height and cannot be dug into.
Shader "NailIt/WarmWater"
{
    Properties
    {
        _ShallowColor("Shallow", Color) = (0.52,0.80,0.70,1)
        _DeepColor("Deep", Color) = (0.13,0.40,0.47,1)
        _FoamColor("Foam", Color) = (1,0.96,0.86,1)
        _SkyTint("Sky Tint", Color) = (1,0.84,0.66,1)
        _DepthRange("Depth Range", Float) = 0.55
        _FoamWidth("Foam Width", Float) = 0.12
        [NoScaleOffset] _PaintNoise("Noise", 2D) = "gray" {}
        _RippleScale("Ripple Scale", Float) = 0.45
        _RippleSpeed("Ripple Speed", Float) = 0.04
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_PaintNoise); SAMPLER(sampler_PaintNoise);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor, _SkyTint;
                float _DepthRange, _FoamWidth, _RippleScale, _RippleSpeed;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; float shore : TEXCOORD2; };

            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.shore = i.uv.x;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float depth = (1.0 - i.shore * i.shore) * _DepthRange;

                float t = _Time.y * _RippleSpeed;
                float2 uv = i.positionWS.xz * _RippleScale;
                half n1 = SAMPLE_TEXTURE2D(_PaintNoise, sampler_PaintNoise, uv + float2(t, t * 0.6)).g;
                half n2 = SAMPLE_TEXTURE2D(_PaintNoise, sampler_PaintNoise, uv * 1.7 - float2(t * 0.8, -t)).g;
                half ripple = n1 * n2;

                half deep = saturate(depth / _DepthRange);
                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half lit = lerp(0.72, 1.0, sun.shadowAttenuation);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = pow(1.0 - saturate(viewDir.y), 3.0);
                color = lerp(color, _SkyTint.rgb, fresnel * 0.4) * lit;

                // Painted glints instead of real reflections.
                half sparkle = smoothstep(0.42, 0.46, ripple) * sun.shadowAttenuation;
                color += sun.color * sparkle * 0.25;

                half foam = smoothstep(1.0 - _FoamWidth, 1.0 - _FoamWidth * 0.3, i.shore + (n1 - 0.5) * 0.08);
                color = lerp(color, _FoamColor.rgb, foam * 0.85);

                half alpha = saturate(lerp(0.82, 0.96, deep) + foam);
                return half4(MixFog(color, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}
