#ifndef NAILIT_WIND_SWAY_INCLUDED
#define NAILIT_WIND_SWAY_INCLUDED

// Set globally by GlobalWind.cs. With no GlobalWind in the scene, strength is 0 and nothing moves.
float4 _NailWindDirection; // xz = world direction
float _NailWindSpeed;
float _NailWindStrength;

// Kept outside UnityPerMaterial because URP's input files own that buffer.
// This opts these materials out of the SRP Batcher; GPU instancing still works.
float _SwayHeight; // world height at which the sway reaches full strength
float _SwayAmount; // world distance moved at full strength
float _SwaySpeed; // multiplies the global wind speed, so big plants can move slower
float _ColorVariation; // how far the tint moves away from the base colour

float3 ApplyWindSway(float3 positionOS)
{
    if (_NailWindStrength <= 0.0)
        return positionOS;

    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

    // Bases stay planted and the tips move the most.
    float mask = saturate((positionWS.y - originWS.y) / max(_SwayHeight, 0.001));
    mask *= mask;

    // Offsets each plant so they don't all move in unison.
    float phase = dot(originWS.xz, float2(0.37, 0.61));
    float time = _Time.y * _NailWindSpeed * _SwaySpeed;
    float wave = sin(time + phase) + 0.5 * sin(time * 2.3 + phase * 1.7);

    positionWS.xz += _NailWindDirection.xz * wave * mask * _SwayAmount * _NailWindStrength;
    return TransformWorldToObject(positionWS);
}

float WindVariationHash(float2 cell)
{
    return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
}

float WindVariationNoise(float2 position)
{
    float2 cell = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3.0 - 2.0 * blend);

    float bottom = lerp(WindVariationHash(cell), WindVariationHash(cell + float2(1.0, 0.0)), blend.x);
    float top = lerp(WindVariationHash(cell + float2(0.0, 1.0)), WindVariationHash(cell + float2(1.0, 1.0)), blend.x);
    return lerp(bottom, top, blend.y);
}

// Uses the plant's origin so each plant keeps one colour. Broad patches plus a smaller break-up.
half3 GetWindColorTint()
{
    float2 originXZ = TransformObjectToWorld(float3(0.0, 0.0, 0.0)).xz;
    float noise = WindVariationNoise(originXZ * 0.12) * 0.7 + WindVariationNoise(originXZ * 0.45) * 0.3;
    float signedNoise = clamp((noise - 0.5) * 3.2, -1.0, 1.0);

    // High values lean warm and bright, low values lean cool and dark.
    return (half3)(1.0 + _ColorVariation * signedNoise * float3(1.0, 0.35, -0.6));
}

#endif
