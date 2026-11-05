Shader "NailIt/WarmStylized"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [NoScaleOffset] _PaintNoise("Paint Noise", 2D) = "gray" {}
        _PaintStrength("Paint Strength", Range(0,1)) = 0.6
        _PaintScale("Paint Scale", Float) = 0.35
        _ShadowTint("Shadow Fill", Color) = (0.2,0.14,0.3,1)
        _RampOffset("Ramp Offset", Range(-1,1)) = 0.05
        _RampSoft("Ramp Softness", Range(0.01,1)) = 0.22
        _RimColor("Rim Color", Color) = (0.18,0.1,0.05,1)
        _RimPower("Rim Power", Range(1,8)) = 4
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_WIND)] _Wind("Wind Sway", Float) = 0
        _WindAmount("Wind Amount (m)", Float) = 0.06
        _WindSpeed("Wind Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma shader_feature_local _ALPHATEST_ON
        #pragma shader_feature_local_vertex _WIND
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex WarmVert
            #pragma fragment WarmFrag
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #include "WarmStylized.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "WarmStylized.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "WarmStylized.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #include "WarmStylized.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
