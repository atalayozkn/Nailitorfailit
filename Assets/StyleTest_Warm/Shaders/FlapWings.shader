// Butterflies and birds: one quad per wing, flapped in the vertex shader. No textures;
// the wing outline is drawn from UVs. Phase comes from the object position, so instances differ.
Shader "NailIt/FlapWings"
{
    Properties
    {
        _Color("Wing Color", Color) = (1,0.8,0.25,1)
        _EdgeColor("Edge Color", Color) = (0.3,0.18,0.08,1)
        _Shape("Shape (0 butterfly, 1 bird)", Range(0,1)) = 0
        _FlapSpeed("Flap Speed", Float) = 16
        _FlapAngle("Flap Angle", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #pragma target 3.5
        #pragma multi_compile_instancing
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _EdgeColor;
            half _Shape;
            float _FlapSpeed, _FlapAngle;
        CBUFFER_END

        struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

        float3 Flap(float3 p)
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float phase = frac(dot(origin, float3(12.9898, 78.233, 37.719))) * 6.2831;
            float a = sin(_Time.y * _FlapSpeed + phase) * _FlapAngle + 0.25;
            float span = abs(p.x);
            return float3(sign(p.x) * span * cos(a), p.y + span * sin(a), p.z);
        }

        // 1 inside the wing or body silhouette, 0 outside.
        half Silhouette(float2 uv, out half edge)
        {
            float2 p = float2(abs(uv.x - 0.5) * 2.0, uv.y);
            half body = step(p.x, 0.07) * step(0.12, uv.y) * step(uv.y, 0.9);
            half wing;
            if (_Shape < 0.5)
            {
                float upper = length((p - float2(0.5, 0.64)) / float2(0.5, 0.34));
                float lower = length((p - float2(0.4, 0.3)) / float2(0.38, 0.26));
                float d = min(upper, lower);
                wing = step(d, 1.0);
                edge = smoothstep(0.7, 0.95, d);
            }
            else
            {
                float chord = 0.26 * (1.0 - p.x * 0.75);
                float d = abs(uv.y - 0.55 + p.x * 0.18) / max(chord, 0.001);
                wing = step(d, 1.0) * step(p.x, 0.98);
                edge = smoothstep(0.6, 1.0, p.x);
            }
            return saturate(wing + body);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionWS = TransformObjectToWorld(Flap(i.positionOS.xyz));
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = i.uv;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half edge;
                clip(Silhouette(i.uv, edge) - 0.5);
                half3 c = lerp(_Color.rgb, _EdgeColor.rgb, edge);
                if (abs(i.uv.x - 0.5) < 0.035) c = _EdgeColor.rgb * 0.6;
                Light sun = GetMainLight();
                c *= SampleSH(half3(0, 1, 0)) + sun.color * 0.6;
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float3 _LightDirection;
            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                o.positionWS = TransformObjectToWorld(Flap(i.positionOS.xyz));
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS, float3(0, 1, 0), _LightDirection)));
                o.uv = i.uv;
                return o;
            }
            half frag(V i) : SV_Target
            {
                half edge;
                clip(Silhouette(i.uv, edge) - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }
}
