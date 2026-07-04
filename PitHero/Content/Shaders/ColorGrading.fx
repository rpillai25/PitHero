// Color Grading Shader with dual-LUT blending - FNA/XNA compatible (Shader Model 3.0)
// Pixel-shader only: Nez Batcher applies its own SpriteEffect vertex pass.

float Size       = 16;
float SizeRoot   = 4;
float InvLUTSize = 0.015625; // 1 / (SizeRoot * Size)
float BlendFactor = 0;       // 0 = full LUT A, 1 = full LUT B

// Input scene texture - bound automatically to s0 by Nez Batcher
sampler2D InputSampler : register(s0);

texture LUTTextureA;
sampler2D LUTSamplerA = sampler_state
{
    Texture   = <LUTTextureA>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU  = Clamp;
    AddressV  = Clamp;
};

texture LUTTextureB;
sampler2D LUTSamplerB = sampler_state
{
    Texture   = <LUTTextureB>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU  = Clamp;
    AddressV  = Clamp;
};

struct PSInput
{
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float4 PSApplyLUT(PSInput input) : COLOR0
{
    // As a sprite Material this samples each tile's atlas texture, so we must preserve
    // the source alpha (transparent tile pixels stay transparent) and honor the vertex
    // color (layer opacity / tint). LUT indexing still runs on the source RGB.
    float4 src = tex2D(InputSampler, input.TexCoord);

    float blue      = src.b * (Size - 1);
    float blueFloor = floor(blue);
    float blueFrac  = frac(blue);
    float blueNext  = min(blueFloor + 1, Size - 1);

    float red   = src.r * (Size - 1);
    float green = src.g * (Size - 1);

    float col0 = fmod(blueFloor, SizeRoot);
    float row0 = floor(blueFloor / SizeRoot);
    float col1 = fmod(blueNext, SizeRoot);
    float row1 = floor(blueNext / SizeRoot);

    float2 uv0 = float2(
        (col0 * Size + red   + 0.5) * InvLUTSize,
        (row0 * Size + green + 0.5) * InvLUTSize
    );
    float2 uv1 = float2(
        (col1 * Size + red   + 0.5) * InvLUTSize,
        (row1 * Size + green + 0.5) * InvLUTSize
    );

    float4 gradedA = lerp(tex2D(LUTSamplerA, uv0), tex2D(LUTSamplerA, uv1), blueFrac);
    float4 gradedB = lerp(tex2D(LUTSamplerB, uv0), tex2D(LUTSamplerB, uv1), blueFrac);
    float3 graded  = lerp(gradedA, gradedB, BlendFactor).rgb;

    // Keep the source alpha and apply the vertex tint so transparency and layer
    // opacity survive when this runs as a per-sprite tilemap material.
    return float4(graded, src.a) * input.Color;
}

technique ApplyLUT
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSApplyLUT();
    }
}
