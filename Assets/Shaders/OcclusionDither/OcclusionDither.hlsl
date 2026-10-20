#ifndef OCCLUSION_DITHER_INCLUDED
#define OCCLUSION_DITHER_INCLUDED

// Globals set by CameraDitherHelper (Shader.SetGlobal*), so they are not part of the
// per-material CBUFFER and the dither shaders stay SRP Batcher compatible.

// Max fraction of pixels removed inside the circle. 0 = solid, 1 = fully see-through.
float _OcclusionDitherAmount;
// xy = player position in viewport space (0..1), z = player view depth (distance along camera forward).
float4 _OcclusionDitherCenter;
// Circle radius and soft edge width, as a fraction of screen height.
float _OcclusionDitherRadius;
float _OcclusionDitherSoftness;

static const float OCCLUSION_BAYER_4X4[16] =
{
     0.0,  8.0,  2.0, 10.0,
    12.0,  4.0, 14.0,  6.0,
     3.0, 11.0,  1.0,  9.0,
    15.0,  7.0, 13.0,  5.0
};

// Ordered dither threshold for a pixel position (SV_POSITION.xy in the fragment stage).
float OcclusionDitherThreshold(float2 pixelPosition)
{
    uint2 p = uint2(pixelPosition) % 4;
    return (OCCLUSION_BAYER_4X4[p.y * 4 + p.x] + 0.5) / 16.0;
}

// 1 inside the circle around the player (fading to 0 over the soft edge), 0 outside
// and for pixels that are behind the player.
float OcclusionDitherMask(float4 positionCS)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float2 delta = uv - _OcclusionDitherCenter.xy;
    delta.x *= _ScreenParams.x / _ScreenParams.y; // keep the circle round

    float radius = _OcclusionDitherRadius;
    float circle = 1.0 - smoothstep(radius - _OcclusionDitherSoftness, radius, length(delta));

    // In the fragment stage SV_POSITION.w is the view depth (perspective camera).
    float inFront = step(positionCS.w, _OcclusionDitherCenter.z);
    return circle * inFront;
}

// Screen-door dither inside the circle around the player, fully solid elsewhere.
void OcclusionDitherClip(float4 positionCS)
{
    clip(OcclusionDitherThreshold(positionCS.xy) - _OcclusionDitherAmount * OcclusionDitherMask(positionCS));
}

#endif
