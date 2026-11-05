// Drawn over a floor tile as a second material. Water spreads in from organic patches as _Wetness rises: the noise
// runs in world space so wet areas continue across neighbouring tiles instead of showing the tile grid.
Shader "NailIt/TileWetOverlay"
{
    Properties
    {
        _Wetness ("Wetness", Range(0, 1)) = 0
        _WetColor ("Wet Color", Color) = (0.05, 0.1, 0.16, 1)
        _WetStrength ("Wet Strength", Range(0, 1)) = 0.55
        _SheenColor ("Sheen Color", Color) = (0.65, 0.85, 1, 1)
        _NoiseScale ("Noise Scale", Float) = 2.2
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "WetOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Wetness;
                half4 _WetColor;
                float _WetStrength;
                half4 _SheenColor;
                float _NoiseScale;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }
            float Fractal(float2 p)
            {
                return Noise(p) * 0.6 + Noise(p * 2.1 + 7.3) * 0.28 + Noise(p * 4.3 + 3.1) * 0.12;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float noise = Fractal(input.positionWS.xz * _NoiseScale);
                // Rising wetness floods from the low points of the noise until the whole tile is covered.
                float reach = _Wetness * (1.0 + _EdgeSoftness);
                float mask = smoothstep(noise, noise + _EdgeSoftness, reach);
                // Only surfaces facing up hold water.
                mask *= saturate(input.normalWS.y * 2.0 - 0.5);

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(viewDir + normalize(float3(0.35, 0.8, 0.25)));
                float sheen = pow(saturate(dot(normalize(input.normalWS), halfDir)), 40.0) * smoothstep(0.35, 0.9, mask);

                half3 color = lerp(_WetColor.rgb, _SheenColor.rgb, sheen);
                return half4(color, saturate(mask * _WetStrength + sheen * 0.5));
            }
            ENDHLSL
        }
    }
}
