// Drawn over a fragile floor tile (wood or glass) as an extra material slot. An impact crack starts at the tile centre:
// jagged rays of varying width grow outward as _Crack rises, and a fine hairline web fills in near the end, so
// the whole tile is cracked just before it breaks. Every tile gets its own pattern. TileCracks sets _Crack per tile.
Shader "NailIt/TileCrackOverlay"
{
    Properties
    {
        _Crack ("Crack Amount", Range(0, 1)) = 0
        _CrackColor ("Crack Color", Color) = (0.16, 0.09, 0.04, 0.95)
        _EdgeColor ("Crack Edge Color", Color) = (1, 1, 1, 0)
        _CellScale ("Cells Per Metre", Float) = 4
        _LineWidth ("Line Width", Range(0.005, 0.2)) = 0.045
        _EdgeWidth ("Edge Highlight Width", Range(0, 0.2)) = 0.04
        _TileRadius ("Tile Radius (m)", Float) = 0.72
        _RayCount ("Main Crack Rays", Range(3, 16)) = 9
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-9" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "CrackOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Crack;
                half4 _CrackColor;
                half4 _EdgeColor;
                float _CellScale;
                float _LineWidth;
                float _EdgeWidth;
                float _TileRadius;
                float _RayCount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float Hash1(float2 p) { return frac(sin(dot(p, float2(41.3, 289.1))) * 15731.743); }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash1(i), Hash1(i + float2(1, 0)), f.x), lerp(Hash1(i + float2(0, 1)), Hash1(i + float2(1, 1)), f.x), f.y);
            }

            // Distance to the nearest cell edge (F2 - F1 style, using the true bisector) and the nearest cell id.
            float Voronoi(float2 p, out float2 cellId)
            {
                float2 baseCell = floor(p);
                float2 f = frac(p);
                float best = 8.0;
                float2 bestOffset = 0;
                cellId = baseCell;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = g + Hash2(baseCell + g) - f;
                    float d = dot(o, o);
                    if (d < best) { best = d; bestOffset = o; cellId = baseCell + g; }
                }
                float edge = 8.0;
                for (int y2 = -2; y2 <= 2; y2++)
                for (int x2 = -2; x2 <= 2; x2++)
                {
                    float2 g = float2(x2, y2);
                    float2 o = g + Hash2(floor(p) + g) - f;
                    float2 diff = o - bestOffset;
                    if (dot(diff, diff) > 0.00001)
                        edge = min(edge, dot(0.5 * (bestOffset + o), normalize(diff)));
                }
                return edge;
            }

            // How much of a pixel a line of the given width (metres) covers at distance d from its centre line.
            // Lines narrower than a pixel keep a 1 px footprint but fade, so they never flicker.
            float LineCoverage(float d, float width, float px)
            {
                float halfWidth = max(width, px) * 0.5;
                return (1.0 - smoothstep(-px * 0.5, px * 0.5, d - halfWidth)) * saturate(width / px);
            }
            // The chipped band just outside a line.
            float EdgeCoverage(float d, float width, float edge, float px)
            {
                return 1.0 - smoothstep(-px * 0.5, px * 0.5, d - width * 0.5 - edge);
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
                if (_Crack <= 0.0) discard;

                // Impact crack: everything is measured from the tile centre, so the crack starts there and grows outward.
                float2 center = TransformObjectToWorld(float3(0, 0, 0)).xz;
                float2 local = input.positionWS.xz - center;
                float r = length(local);
                float seed = Hash1(floor(center * 7.31) + 0.5); // every tile gets its own pattern
                float reach = _Crack * _TileRadius * 1.15;   // how far the crack has spread (metres)
                if (r > reach + 0.05) discard;

                // Pixel size in metres: lines thinner than this fade out instead of breaking up (no flicker at distance).
                float px = max(length(fwidth(input.positionWS.xz)) * 0.7, 0.0002);

                // Angle with a smooth bend plus sharp kinks along the radius, so rays zig-zag like real fractures.
                float angle = atan2(local.y, local.x) / TWO_PI + 0.5;                 // 0..1
                angle += (ValueNoise(float2(r * 9.0, seed * 31.0)) - 0.5) * 0.045;
                angle += (abs(ValueNoise(float2(r * 26.0, seed * 57.0 + 3.0)) - 0.5) * 2.0 - 0.5) * 0.03;

                // Rays: _RayCount cracks at jittered angles, each with its own length. A few are major cracks that gape
                // open; along every ray the width swells and pinches, so it never looks like an even pen line.
                float taper = lerp(1.0, 0.35, saturate(r / max(reach, 0.001)));     // widest at the impact
                float sector = floor(angle * _RayCount);
                float lineMask = 0.0;
                float edgeMask = 0.0;
                for (int k = 0; k <= 1; k++)
                {
                    float id = fmod(sector + k + _RayCount, _RayCount);
                    float rayAngle = (sector + k + (Hash1(float2(id, seed)) - 0.5) * 0.7) / _RayCount;
                    float rayLength = reach * lerp(0.55, 1.0, Hash1(float2(id * 1.7, seed + 9.0)));
                    if (r > rayLength) continue;

                    float major = Hash1(float2(id * 2.3, seed + 2.0)) > 0.62 ? 2.2 : 0.7;
                    float bulge = 0.2 + 2.0 * pow(ValueNoise(float2(r * 16.0 + id * 7.1, seed * 13.0)), 1.6);
                    float tip = saturate((rayLength - r) / 0.06);                        // pinches to a point at its end
                    float width = _LineWidth * major * bulge * taper * tip;

                    float d = abs(angle - rayAngle) * TWO_PI * r;                         // arc distance to the ray (metres)
                    lineMask = max(lineMask, LineCoverage(d, width, px));
                    edgeMask = max(edgeMask, EdgeCoverage(d, width, _EdgeWidth * major, px));
                }

                // Fine web: hairline Voronoi branches filling in late, close behind the crack front.
                float2 p = input.positionWS.xz * _CellScale;
                p += (float2(ValueNoise(p * 0.9), ValueNoise(p * 0.9 + 17.3)) - 0.5) * 0.7;
                float2 cellId;
                float distWeb = Voronoi(p, cellId) / _CellScale;
                float webShown = smoothstep(0.45, 0.8, _Crack) * step(r, reach * 0.85) * step(0.45, ValueNoise(local * 6.0 + seed * 11.0));
                lineMask = max(lineMask, LineCoverage(distWeb, _LineWidth * 0.3, px) * webShown * 0.8);

                edgeMask = saturate(edgeMask - lineMask);

                float front = 1.0 - smoothstep(reach - 0.03, reach + 0.03, r);          // soft crack tip
                float up = saturate(input.normalWS.y * 2.0 - 0.5);                      // only the walking surface cracks

                float lineWeight = _CrackColor.a * lineMask;
                float edgeWeight = _EdgeColor.a * edgeMask;
                float coverage = lineWeight + edgeWeight;
                float alpha = coverage * front * up;
                if (alpha <= 0.001) discard;
                half3 color = (_CrackColor.rgb * lineWeight + _EdgeColor.rgb * edgeWeight) / max(coverage, 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
