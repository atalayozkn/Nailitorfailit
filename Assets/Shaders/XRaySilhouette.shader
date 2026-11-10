// Draws a mesh only where something else covers it (roof, wall, floor tile): a flat tint plus a rim,
// so small gameplay objects such as sockets and pipes stay readable under the house.
// Used by XRayOverlay on a hidden child copy of each mesh; colour comes per renderer through a property block.
Shader "NailIt/XRaySilhouette"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 0.25)
        _RimPower("Rim Power", Range(0.5, 8)) = 2
        _RimBoost("Rim Boost", Range(0, 2)) = 0.8
    }
    SubShader
    {
        // After opaques and the dithered occluders, so the depth buffer holds whatever covers the object.
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "XRay"
            Tags { "LightMode"="UniversalForward" }
            ZTest Greater
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower;
                half _RimBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                half alpha = saturate(_Color.a * (1.0 + rim * _RimBoost * 2.0));
                return half4(_Color.rgb + rim * _RimBoost * 0.3, alpha);
            }
            ENDHLSL
        }
    }
}
