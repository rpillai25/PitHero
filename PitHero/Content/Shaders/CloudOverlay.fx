// Volumetric Scrolling Cloud Overlay - FNA/XNA compatible (Shader Model 3.0)
// Pixel-shader only: Nez Batcher applies its own SpriteEffect vertex pass.
//
// Samples a small tileable 2-field noise texture (R/G channels are independent noise fields,
// crossfaded via MorphFactor so cloud shapes genuinely morph over time, not just scroll) three
// times at different scales/scroll speeds to build a cheap fBm "density" field. The density is
// thresholded into per-pixel cloud opacity with a soft edge, tinted by time-of-day, and darkened
// slightly in dense cores for a volumetric look. World position is reconstructed per pixel from
// CameraTopLeft/CameraSize so a single camera-bounds quad renders world-anchored clouds under any
// pan/zoom without a giant fixed-size quad.

// s0 (bound by Batcher to the quad texture) intentionally unused.
sampler2D InputSampler : register(s0);

// Wrap+linear sampling MUST live in this sampler block, NOT Material.SamplerState
// (Material.Dispose/finalizer disposes non-default SamplerStates - would kill a shared static).
texture NoiseTexture;
sampler2D NoiseSampler = sampler_state
{
    Texture   = <NoiseTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU  = Wrap;
    AddressV  = Wrap;
};

float2 CameraTopLeft;    // world px, per frame
float2 CameraSize;       // world px, per frame
float  NoiseWorldScale;  // set once
float  Octave2Mult;      // set once
float  Octave3Mult;      // set once
float2 ScrollOffset1;    // pre-wrapped [0,1)
float2 ScrollOffset2;    // pre-wrapped [0,1)
float2 ScrollOffset3;    // pre-wrapped [0,1)
float  MorphFactor;      // 0..1 crossfade between R and G noise fields
float  CoverageThreshold;
float  CoverageSoftness;
float4 CloudColor;       // rgb = time-of-day tint, a = max opacity

struct PSInput
{
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float SampleField(float2 uv)
{
    float4 n = tex2D(NoiseSampler, uv);
    return lerp(n.r, n.g, MorphFactor);
}

float4 PSCloudOverlay(PSInput input) : COLOR0
{
    float2 wp = CameraTopLeft + input.TexCoord * CameraSize;

    float2 uv1 = wp * (NoiseWorldScale) + ScrollOffset1;
    float2 uv2 = wp * (NoiseWorldScale * Octave2Mult) + ScrollOffset2 + float2(0.37, 0.71);
    float2 uv3 = wp * (NoiseWorldScale * Octave3Mult) + ScrollOffset3 + float2(0.71, 0.37);

    float n1 = SampleField(uv1);
    float n2 = SampleField(uv2);
    float n3 = SampleField(uv3);

    float density = 0.5 * n1 + 0.35 * n2 + 0.15 * n3;

    float a = smoothstep(CoverageThreshold, CoverageThreshold + CoverageSoftness, density) * CloudColor.a;

    // Darken dense cores for a volumetric look — wispy edges stay bright, cores read heavier.
    float core = smoothstep(CoverageThreshold + CoverageSoftness, CoverageThreshold + CoverageSoftness * 2.0, density);
    float3 rgb = CloudColor.rgb * (1.0 - core * 0.15);

    // Premultiplied alpha - Material's default BlendState.AlphaBlend expects it.
    return float4(rgb * a, a);
}

technique CloudOverlay
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCloudOverlay();
    }
}
