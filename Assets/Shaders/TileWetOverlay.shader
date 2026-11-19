// Drawn over a floor tile as a second material. Water spreads in from organic patches as _Wetness rises: the noise
// runs in world space so wet areas continue across neighbouring tiles instead of showing the tile grid.
// The water must read the same from every camera angle: the darkening, a blue water tint, a lighter rim around each
// patch and a soft constant gloss do not depend on the view. The sky reflection and the sun highlight (real main
// light direction, broad) only add extra shine on top.
Shader "NailIt/TileWetOverlay"
{
    Properties
    {
        _Wetness ("Wetness", Range(0, 1)) = 0
        _WetColor ("Wet Color (darkening)", Color) = (0.05, 0.1, 0.16, 1)
        _WetStrength ("Wet Strength", Range(0, 1)) = 0.55
        _WaterTint ("Water Tint", Color) = (0.3, 0.55, 0.78, 1)
        _TintAmount ("Water Tint Amount", Range(0, 1)) = 0.55
        _RimColor ("Puddle Rim Color", Color) = (0.8, 0.93, 1, 1)
        _RimStrength ("Puddle Rim Strength", Range(0, 1)) = 0.35
        _Gloss ("Constant Gloss", Range(0, 1)) = 0.12
        _SheenColor ("Sheen Color", Color) = (0.65, 0.85, 1, 1)
        _ReflectStrength ("Sky Reflection", Range(0, 1)) = 0.35
        _SpecStrength ("Sun Highlight", Range(0, 2)) = 0.6
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
            // Premultiplied: the darkened water body blends over the tile, the shine adds on top of it.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Wetness;
                half4 _WetColor;
                float _WetStrength;
                half4 _WaterTint;
                half _TintAmount;
                half4 _RimColor;
                half _RimStrength;
                half _Gloss;
                half4 _SheenColor;
                half _ReflectStrength;
                half _SpecStrength;
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

                float3 n = normalize(input.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));

                // The middle of a patch holds standing water; the band around its edge is the puddle rim.
                half core = smoothstep(0.35, 0.9, mask);
                half rim = saturate(mask * (1.0 - mask) * 4.0) * step(0.001, mask);

                // View independent: darkened, blue tinted water body.
                half bodyAlpha = mask * _WetStrength;
                half3 body = lerp(_WetColor.rgb, _WaterTint.rgb, core * _TintAmount);

                // View independent shine: soft constant gloss over the water and a lighter rim around each patch.
                half3 shine = _SheenColor.rgb * (_Gloss * core) + _RimColor.rgb * (rim * _RimStrength);

                // Extras that change with the angle: sky reflection (stronger at grazing angles) and a broad sun highlight.
                half fresnel = 0.25 + 0.75 * pow(1.0 - saturate(dot(n, viewDir)), 3.0);
                half3 sky = GlossyEnvironmentReflection(reflect(-viewDir, n), 0.15, 1.0);
                shine += sky * (fresnel * _ReflectStrength * core);
                Light sun = GetMainLight();
                half spec = pow(saturate(dot(n, normalize(sun.direction + viewDir))), 12.0);
                shine += sun.color * _SheenColor.rgb * (spec * _SpecStrength * core);

                return half4(body * bodyAlpha + shine * _Wetness, saturate(bodyAlpha));
            }
            ENDHLSL
        }
    }
}
