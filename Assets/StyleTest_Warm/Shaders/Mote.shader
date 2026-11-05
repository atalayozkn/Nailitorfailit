// Soft additive dot for pollen and dust particles. No texture.
Shader "NailIt/Mote"
{
    Properties { _Tint("Tint", Color) = (1,0.9,0.65,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Tint; CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.color = i.color; o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                half d = saturate(1.0 - length(i.uv * 2.0 - 1.0));
                return half4(_Tint.rgb * i.color.rgb, d * d * i.color.a * _Tint.a);
            }
            ENDHLSL
        }
    }
}
