# Geometry snap HLSL (portable)

Paste into the host project under a neutral path (e.g. `Assets/Rendering/PixelSnap/`). Rename the include guard if needed; keep behaviour identical.

## `PixelSnapVertex.hlsl`

```hlsl
#ifndef PIXEL_SNAP_VERTEX_INCLUDED
#define PIXEL_SNAP_VERTEX_INCLUDED

#ifndef PIXEL_SNAP_DEFAULT_RES
#define PIXEL_SNAP_DEFAULT_RES float2(960.0, 540.0)
#endif

float2 PixelSnapResolution(float2 pixelResolution)
{
    float2 res = pixelResolution;
    if (res.x < 1.0 || res.y < 1.0)
        res = PIXEL_SNAP_DEFAULT_RES;
    return res;
}

/// Snap clip-space XY to the center of a pixel on `pixelResolution` grid.
/// amount 0 = off, 1 = full. Does not touch texture sampling.
float4 SnapClipToPixelGrid(float4 positionCS, float2 pixelResolution, float amount)
{
    amount = saturate(amount);
    if (amount <= 0.0)
        return positionCS;

    float2 res = PixelSnapResolution(pixelResolution);
    float invW = positionCS.w != 0.0 ? rcp(positionCS.w) : 0.0;
    float2 ndc = positionCS.xy * invW;
    float2 pixel = (ndc * 0.5 + 0.5) * res;
    float2 snappedPixel = floor(pixel) + 0.5;
    float2 snappedNdc = (snappedPixel / res) * 2.0 - 1.0;
    float2 lerped = lerp(ndc, snappedNdc, amount);
    positionCS.xy = lerped * positionCS.w;
    return positionCS;
}

#endif
```

## Shader wiring (URP Sprite Lit pattern)

After the stock sprite vertex helper (`CommonLitVertex` / `CommonUnlitVertex` / equivalent):

```hlsl
o.positionCS = SnapClipToPixelGrid(o.positionCS, _PixelResolution.xy, _PixelSnap);
// If the pass stores lighting / screen UV from clip:
o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
```

Material properties:

```text
_PixelResolution  (Vector)  e.g. (960, 540, 0, 0)  — match Pixel Perfect reference when used
_PixelSnap        (Float)   0..1, default 1 on sprite mats, never on text mats
```

Put `_PixelResolution` and `_PixelSnap` in `UnityPerMaterial` next to `_Color` so SRP Batcher layout stays stable across passes (Lit / Normals / Forward).

## Assignment

1. Create material from the Lit (or Unlit) snap shader.
2. Renderer2D → Default Material Type = Custom → that material.
3. Replace serialized default sprite material GUIDs on scenes/prefabs, or assign in Editor to every `SpriteRenderer` still on stock Sprite-Lit/Unlit.
4. Leave TMP / UI font materials unchanged.
