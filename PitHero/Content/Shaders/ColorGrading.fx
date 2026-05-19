// Color Grading Shader with LUTs - FNA/XNA compatible (Shader Model 3.0)
// Adapted from TheKosmonaut 2017 (kosmonaut3d@googlemail.com)
// Pixel-shader only: Nez Batcher applies its own SpriteEffect vertex pass.

float Size     = 16;
float SizeRoot = 4;
float InvLUTSize = 0.015625; // 1 / (SizeRoot * Size), updated from C# when Size/SizeRoot change

// Input scene texture - bound automatically to s0 by Nez Batcher
sampler2D InputSampler : register(s0);

// LUT texture - bound via Effect.Parameters["LUTTexture"] in C#
texture LUTTexture;
sampler2D LUTSampler = sampler_state
{
    Texture   = <LUTTexture>;
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

// Trilinear LUT lookup: hardware bilinear handles R+G within a blue slice;
// we manually lerp between adjacent blue slices.
float4 PSApplyLUT(PSInput input) : COLOR0
{
    float4 color = tex2D(InputSampler, input.TexCoord);

    float blue      = color.b * (Size - 1);
    float blueFloor = floor(blue);
    float blueFrac  = frac(blue);
    float blueNext  = min(blueFloor + 1, Size - 1);

    float red   = color.r * (Size - 1);
    float green = color.g * (Size - 1);

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

    return lerp(tex2D(LUTSampler, uv0), tex2D(LUTSampler, uv1), blueFrac);
}

technique ApplyLUT
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSApplyLUT();
    }
}
