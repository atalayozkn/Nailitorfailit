// Stamina bar fill. StaminaBarVisual drives the state through material properties:
// tint, sliding diagonal stripes (direction = draining or refilling), a sweeping sheen (energised),
// a pulse (empty) and a glow on the leading edge (refilling). UI-compatible: stencil masks and RectMask2D work.
Shader "NailIt/UI/StaminaBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StripeSpeed ("Stripe Speed (signed)", Float) = 0
        _StripeAmount ("Stripe Amount", Range(0,1)) = 0
        _StripeWidth ("Stripe Width (px)", Float) = 14
        _SheenAmount ("Sheen Amount", Range(0,1)) = 0
        _SheenSpeed ("Sheen Speed", Float) = 0.6
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0
        _PulseSpeed ("Pulse Speed", Float) = 6
        _EdgeGlow ("Leading Edge Glow", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _StripeSpeed, _StripeAmount, _StripeWidth;
            float _SheenAmount, _SheenSpeed;
            float _PulseAmount, _PulseSpeed;
            float _EdgeGlow;
            // Set by the canvas, same as UI/Default: vertex colours arrive in gamma space.
            float _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace()) v.color.rgb = UIGammaToLinear(v.color.rgb);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                float2 p = i.worldPosition.xy; // canvas units, so stripes keep their size as the fill grows

                // Diagonal stripes, sliding left while draining and right while refilling.
                float s = frac((p.x + p.y) / (_StripeWidth * 2.0) - _Time.y * _StripeSpeed);
                float stripe = smoothstep(0.42, 0.5, s) - smoothstep(0.92, 1.0, s);
                col.rgb = lerp(col.rgb, col.rgb * 1.22 + 0.04, stripe * _StripeAmount);

                // A soft highlight band sweeping across while energised.
                float band = frac((p.x - p.y * 0.6) / 420.0 - _Time.y * _SheenSpeed);
                float sheen = exp(-pow((band - 0.5) * 9.0, 2.0));
                col.rgb += sheen * _SheenAmount * 0.55;

                // Pulse while empty.
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                col.rgb = lerp(col.rgb, col.rgb * 1.45 + 0.08, pulse * _PulseAmount);

                // Glow on the leading (right) edge while refilling; the sliced sprite's last border maps to uv.x ~1.
                col.rgb += smoothstep(0.7, 1.0, i.texcoord.x) * _EdgeGlow * 0.45;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
