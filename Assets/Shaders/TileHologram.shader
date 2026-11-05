// A floor tile that isn't finished yet: the real mesh and texture as a see-through hologram. As _Reveal rises the hologram
// burns away along world-space noise and the solid tile shows in its place. Noise runs in world space so the pattern
// doesn't repeat tile by tile. BlueprintVisual drives _Reveal, _AlphaScale and _Highlight.
Shader "NailIt/TileHologram"
{
    Properties
    {
        [MainTexture] _BaseMap ("Tile Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tile Color", Color) = (1, 1, 1, 1)

        [Header(Hologram)]
        _HoloColor ("Hologram Color", Color) = (0.25, 0.8, 1, 1)
        _TextureKeep ("Original Look Kept", Range(0, 1)) = 0.6
        _HoloAlpha ("Opacity", Range(0, 1)) = 0.55
        _AlphaScale ("Opacity Scale", Range(0, 1)) = 1
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.6
        _ScanDensity ("Scanline Density", Float) = 28
        _ScanSpeed ("Scanline Speed", Float) = 0.6
        _ScanStrength ("Scanline Strength", Range(0, 1)) = 0.3

        [Header(Reveal)]
        _Reveal ("Reveal", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Float) = 7
        _EdgeWidth ("Edge Width", Range(0.005, 0.2)) = 0.05
        [HDR] _EdgeColor ("Edge Color", Color) = (0.6, 1.6, 2, 1)

        [Header(Highlight)]
        _Highlight ("Highlight", Range(0, 1)) = 0
        _HighlightColor ("Highlight Color", Color) = (0.95, 1, 0.05, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _HoloColor;
                float _TextureKeep;
                float _HoloAlpha;
                float _AlphaScale;
                float _RimPower;
                float _RimStrength;
                float _ScanDensity;
                float _ScanSpeed;
                float _ScanStrength;
                float _Reveal;
                float _NoiseScale;
                float _EdgeWidth;
                half4 _EdgeColor;
                float _Highlight;
                half4 _HighlightColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Fractal noise bunches around the middle; stretching it keeps the reveal even across 0..1.
                float noise = saturate((Fractal(input.positionWS.xz * _NoiseScale) - 0.2) / 0.6);
                float reveal = _Reveal * (1.0 + _EdgeWidth);
                float solid = smoothstep(noise, noise + 0.02, reveal);
                float edge = _Reveal > 0.0 ? saturate(1.0 - abs(noise - reveal) / _EdgeWidth) * (1.0 - solid) : 0.0;

                // Solid part: close to the finished tile, so swapping to the real material at the end isn't a big jump.
                Light mainLight = GetMainLight();
                half3 lighting = SampleSH(normalWS) + mainLight.color * saturate(dot(normalWS, mainLight.direction));
                half3 solidColor = albedo.rgb * lighting;

                // Hologram part.
                float rim = pow(1.0 - saturate(dot(normalWS, viewDir)), _RimPower) * _RimStrength;
                float scan = 1.0 - _ScanStrength * (0.5 + 0.5 * sin((input.positionWS.y + input.positionWS.z) * _ScanDensity + _Time.y * _ScanSpeed * TWO_PI));
                half3 holoColor = lerp(_HoloColor.rgb, albedo.rgb * _HoloColor.rgb * 1.6 + albedo.rgb * 0.3, _TextureKeep);
                holoColor = holoColor * scan + _HoloColor.rgb * rim;
                float holoAlpha = saturate(_HoloAlpha * scan + rim) * _AlphaScale;

                holoColor = lerp(holoColor, _HighlightColor.rgb, _Highlight * 0.7);
                holoAlpha = lerp(holoAlpha, max(holoAlpha, 0.85), _Highlight);
                solidColor += _HighlightColor.rgb * _Highlight * 0.2;

                half3 color = lerp(holoColor, solidColor, solid) + _EdgeColor.rgb * edge;
                float alpha = saturate(lerp(holoAlpha, 1.0, solid) + edge);
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
