#ifndef TABLE_NINE_PIXEL_SNAP_VERTEX
#define TABLE_NINE_PIXEL_SNAP_VERTEX

// Godot-style pixel feel: snap sprite clip verts to a pixel grid.
// Fullscreen UV snap stays OFF so TMP glyphs are never quantized.

#ifndef TABLE_NINE_PIXEL_SNAP_DEFAULT_RES
#define TABLE_NINE_PIXEL_SNAP_DEFAULT_RES float2(960.0, 540.0)
#endif

float2 TableNineSpritePixelResolution(float2 pixelResolution)
{
    float2 res = pixelResolution;
    if (res.x < 1.0 || res.y < 1.0)
    {
        res = TABLE_NINE_PIXEL_SNAP_DEFAULT_RES;
    }

    return res;
}

float4 TableNineSnapClipToPixelGrid(float4 positionCS, float2 pixelResolution, float pixelSnap)
{
    float amount = saturate(pixelSnap);
    if (amount <= 0.0)
    {
        return positionCS;
    }

    float2 res = TableNineSpritePixelResolution(pixelResolution);
    float invW = positionCS.w != 0.0 ? rcp(positionCS.w) : 0.0;
    float2 ndc = positionCS.xy * invW;
    float2 pixel = (ndc * 0.5 + 0.5) * res;
    float2 snappedPixel = floor(pixel) + 0.5;
    float2 snappedNdc = (snappedPixel / res) * 2.0 - 1.0;
    float2 lerpedNdc = lerp(ndc, snappedNdc, amount);
    positionCS.xy = lerpedNdc * positionCS.w;
    return positionCS;
}

#endif
