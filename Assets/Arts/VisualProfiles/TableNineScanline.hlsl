#ifndef TABLE_NINE_SCANLINE_INCLUDED
#define TABLE_NINE_SCANLINE_INCLUDED

float2 TableNinePixelResolution(float4 pixelResolution)
{
    return max(pixelResolution.xy, float2(1.0, 1.0));
}

float3 TableNineApplyScanlines(float3 color, float2 screenUv, float4 pixelResolution, float scanlineEnabled, float scanlineIntensity, float scanlineSpacing)
{
    float2 pixel = floor(saturate(screenUv) * TableNinePixelResolution(pixelResolution));
    float scanPhase = fmod(pixel.y, max(scanlineSpacing, 1.0));
    float scanline = step(0.5, scanPhase);
    return color * (1.0 - scanline * scanlineIntensity * scanlineEnabled);
}

#endif
