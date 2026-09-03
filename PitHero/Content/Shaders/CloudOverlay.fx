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
float  MacroMult;        // set once; < 1 so the macro field's features are much larger
float  GiantMult;        // set once; << 1 so the giant field's features are larger still
float2 ScrollOffset1;    // pre-wrapped [0,1)
float2 ScrollOffset2;    // pre-wrapped [0,1)
float2 ScrollOffset3;    // pre-wrapped [0,1)
float2 ScrollOffsetMacro;// pre-wrapped [0,1)
float2 ScrollOffsetGiant;// pre-wrapped [0,1)
float  MorphFactor;      // 0..1 crossfade between R and G noise fields
float  MorphGain;        // contrast renormalization: 1/sqrt((1-m)^2 + m^2); averaging two independent
                         // fields squashes values toward the mean, which would pulse cloud cover
float  CoverageThreshold;
float  CoverageSoftness;
float  MacroThreshold;   // macro field level above which the density boost ramps in
float  MacroBoost;       // max density added where the macro field crests
float  GiantThreshold;   // giant field gate; set high so giant crests are rare
float  GiantBoost;       // max density added where the giant field crests
float4 CloudColor;       // rgb = time-of-day tint, a = max opacity
float2 DeadZoneMin;      // world px, set once; clouds never render inside [DeadZoneMin, DeadZoneMax]
float2 DeadZoneMax;      // world px, set once
float  DeadZoneFeather;  // world px over which clouds fade back in outside the dead zone

struct PSInput
{
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float SampleField(float2 uv)
{
    float4 n = tex2D(NoiseSampler, uv);
    return 0.5 + (lerp(n.r, n.g, MorphFactor) - 0.5) * MorphGain;
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

    // Occasional large cloud masses: a much larger, slower macro field boosts density only where it
    // crests, merging the small puffs there into one big cloud while leaving the rest of the sky as-is.
    float2 uvM = wp * (NoiseWorldScale * MacroMult) + ScrollOffsetMacro;
    float nM = SampleField(uvM);
    density += MacroBoost * smoothstep(MacroThreshold, 1.0, nM);

    // Rare very large clouds: an even larger, slower field with a high gate, so most of the time it
    // contributes nothing and only occasionally a giant mass drifts through.
    float2 uvG = wp * (NoiseWorldScale * GiantMult) + ScrollOffsetGiant + float2(0.19, 0.53);
    float nG = SampleField(uvG);
    density += GiantBoost * smoothstep(GiantThreshold, 1.0, nG);

    float a = smoothstep(CoverageThreshold, CoverageThreshold + CoverageSoftness, density) * CloudColor.a;

    // Cloud dead zone: fully clear inside the rect (e.g. the tavern), feathering back to full cloud
    // cover over DeadZoneFeather px outside it. Distance-to-rect keeps the corners rounded.
    float2 outside = max(DeadZoneMin - wp, wp - DeadZoneMax);
    float deadDist = length(max(outside, 0.0));
    a *= smoothstep(0.0, DeadZoneFeather, deadDist);

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
