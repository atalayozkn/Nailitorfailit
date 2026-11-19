// Ground outline around an unfinished room (RoomOutline builds it as a LineRenderer).
// Colour comes from the line's vertex colour, so one shared material serves every room.
// Dashes march slowly along the line and the whole line breathes a little so it reads as a guide, not geometry.
Shader "NailIt/RoomOutline"
{
    Properties
    {
        _DashLength("Dash Repeat (line widths)", Float) = 4
        _DashFill("Dash Fill", Range(0.1, 1)) = 0.65
        _DashSpeed("Dash Speed", Float) = 1.5
        _Pulse("Pulse Amount", Range(0, 1)) = 0.25
        _PulseSpeed("Pulse Speed", Float) = 2
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
                float _DashLength;
                half _DashFill;
                float _DashSpeed;
                half _Pulse;
                float _PulseSpeed;
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
                // LineRenderer in Tile mode: uv.x grows by 1 per line width along the line.
                float dash = frac(i.uv.x / max(_DashLength, 0.01) - _Time.y * _DashSpeed / max(_DashLength, 0.01));
                half on = step(dash, _DashFill);
                // Soft across the width so the edges do not alias.
                half across = 1.0 - smoothstep(0.35, 0.5, abs(i.uv.y - 0.5));
                half pulse = 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                return half4(i.color.rgb, i.color.a * on * across * pulse);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
