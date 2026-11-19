// Ground outline around an unfinished room (RoomOutline builds it as a LineRenderer).
// Colour comes from the line's vertex colour, so one shared material serves every room.
// A solid soft-edged rim that slowly fades in and out, so it reads as a calm guide rather than an arcade effect.
Shader "NailIt/RoomOutline"
{
    Properties
    {
        _PulsePeriod("Pulse Period (s)", Float) = 2.5
        _MinAlpha("Faded Opacity", Range(0, 1)) = 0.2
        _EdgeSoftness("Edge Softness", Range(0.01, 0.5)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RoomOutline"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _PulsePeriod;
                half _MinAlpha;
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Soft across the line width, brightest in the middle.
                half across = 1.0 - smoothstep(0.5 - _EdgeSoftness, 0.5, abs(i.uv.y - 0.5));
                // Smooth fade in and out, one full cycle per _PulsePeriod seconds.
                half wave = 0.5 - 0.5 * cos(_Time.y * TWO_PI / max(_PulsePeriod, 0.1));
                half pulse = lerp(_MinAlpha, 1.0, wave);
                return half4(i.color.rgb, i.color.a * across * pulse);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
