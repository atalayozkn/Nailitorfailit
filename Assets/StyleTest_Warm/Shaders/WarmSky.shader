// Three-colour gradient sky with a sun glow. No textures.
Shader "NailIt/WarmSky"
{
    Properties
    {
        _TopColor("Top", Color) = (0.32,0.56,0.86,1)
        _HorizonColor("Horizon", Color) = (1,0.82,0.62,1)
        _BottomColor("Bottom", Color) = (0.72,0.56,0.44,1)
        _SunGlow("Sun Glow", Color) = (1,0.72,0.42,1)
        _SunGlowPower("Sun Glow Power", Range(1,64)) = 10
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _HorizonColor, _BottomColor, _SunGlow;
                half _SunGlowPower;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.dir = i.positionOS.xyz; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 c = d.y > 0 ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(d.y * 1.6), 0.6))
                                  : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-d.y * 4));
                c += _SunGlow.rgb * pow(saturate(dot(d, _MainLightPosition.xyz)), _SunGlowPower) * 0.6;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
