// Hover highlight for interactables. Drawn as an extra additive pass over a copy of the object's mesh,
// so it works on top of any material (Toon, Lit, Unlit, WarmStylized, glass) without changing it.
Shader "NailIt/HoverGlow"
{
    Properties
    {
        _GlowColor("Glow Color", Color) = (1, 0.82, 0.45, 1)
        _Fill("Fill", Range(0, 1)) = 0.4
        _RimPower("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength("Rim Strength", Range(0, 3)) = 1.4
        _PulseSpeed("Pulse Speed", Float) = 4
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0.25
        _Inflate("Inflate (m)", Float) = 0.004
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "HoverGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _Fill;
                half _RimPower;
                half _RimStrength;
                half _PulseSpeed;
                half _PulseAmount;
                float _Inflate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Push out a hair in world space so the glow sits on the surface whatever the object's scale.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * _Inflate;
                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0 - abs(dot(n, v))), _RimPower);
                half pulse = 1.0 - _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                half amount = (_Fill + rim * _RimStrength) * pulse;
                return half4(_GlowColor.rgb * amount, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
