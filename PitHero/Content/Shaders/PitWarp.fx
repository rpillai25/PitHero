// Pit Warp - FNA/XNA compatible (Shader Model 3.0), issue #469
// Full-screen post-process: twists the pit region of the scene render target into a drain while the
// party floats above it (the party is drawn after post-processing, so it is never twisted).
//
// The pit region is a rectangle in render-target UV (Region.xy = centre, Region.zw = half size).
// Inside it, coordinates are normalized to [-1, 1] so the swirl follows the pit's wide shape:
//  - Twist rotates each pixel about the centre by an angle that falls off to zero at the unit
//    circle, so the outer ring and the corners stay continuous with the surrounding map.
//  - Pull shrinks the twisted content toward the centre (sampling from further out); whatever would
//    be sampled from outside the region is the abyss. At Pull = 1 the whole region is abyss, which is
//    when the pit is regenerated behind it.
// Point sampling (the scene's sampler) keeps the twisted pixels crisp.

sampler2D InputSampler : register(s0);

float4 Region;      // xy = region centre (uv), zw = region half size (uv)
float  Twist;       // swirl angle at the centre, radians
float  Pull;        // 0..1 drain amount
float  Spin;        // abyss spiral phase, radians
float3 AbyssColor;  // colour of the drain's depths

struct PSInput
{
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float4 PSPitWarp(PSInput input) : COLOR0
{
    float2 uv = input.TexCoord;
    float2 p = (uv - Region.xy) / Region.zw;
    float edge = max(abs(p.x), abs(p.y));
    if (edge >= 1.0)
        return tex2D(InputSampler, uv);

    float r = length(p);
    float falloff = saturate(1.0 - r);
    float angle = Twist * falloff * falloff;
    float s, c;
    sincos(angle, s, c);
    float2 q = float2(p.x * c - p.y * s, p.x * s + p.y * c);

    // The drain: content shrinks toward the centre as Pull rises
    float zoom = max(1.0 - Pull, 0.0001);
    q /= zoom;

    // Spiral arms in the abyss, turning with Spin
    float arms = 0.65 + 0.35 * sin(atan2(p.y, p.x) * 3.0 + r * 10.0 - Spin);
    float3 abyss = AbyssColor * arms * (0.55 + 0.45 * r);

    if (max(abs(q.x), abs(q.y)) >= 1.0)
        return float4(abyss, 1.0);

    float4 col = tex2D(InputSampler, Region.xy + q * Region.zw);
    // Darken toward the drain as it deepens
    col.rgb = lerp(col.rgb, abyss, saturate(Pull * 0.85));
    return col;
}

technique PitWarp
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSPitWarp();
    }
}
