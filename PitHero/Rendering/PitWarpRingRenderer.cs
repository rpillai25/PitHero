using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    /// <summary>
    /// The ring of blue light that spreads from the wizard orb when it is touched (issue #469), out to
    /// the pit's width before fading. Drawn after the post-processors (so the pit swirl never twists
    /// it) and before the floating party (<see cref="PitWarpPartyRenderer"/>), additively, with the world
    /// camera. Stateless between frames: the presentation pass feeds it once per rendered frame with
    /// <see cref="SetState"/>, from the live <see cref="Services.PitWarpService"/> or, while a recorded
    /// replay frame is on screen, from the frame's HUD record.
    /// <para>
    /// The ring is a chain of short quads around the circle, each a 1-pixel-wide strip of a soft
    /// vertical glow profile stretched along its chord and sampled linearly, so the band stays smooth at
    /// any radius: a wide faint glow plus a thin bright core.
    /// </para>
    /// </summary>
    public sealed class PitWarpRingRenderer : Renderer
    {
        /// <summary>After the post-processors, just before the floating party (50).</summary>
        public const int Order = 49;

        private const int ProfileHeight = 32;
        private const float GlowStrength = 0.55f;
        private const float CoreBandScale = 0.3f;
        private const float SegmentLengthPx = 8f;
        private const int MinSegments = 24;
        private const int MaxSegments = 320;

        private Texture2D _profile;
        private float _x, _y, _radius, _alpha;

        public PitWarpRingRenderer() : base(Order)
        {
            WantsToRenderAfterPostProcessors = true;
            ShouldDebugRender = false;
            Material = new Material(BlendState.Additive) { SamplerState = SamplerState.LinearClamp };
        }

        /// <summary>Sets the ring shown this frame (world pixels). An alpha or radius of zero draws nothing.</summary>
        public void SetState(float x, float y, float radius, float alpha)
        {
            _x = x;
            _y = y;
            _radius = radius;
            _alpha = alpha;
        }

        /// <summary>Width of the glowing band for a radius: thin as it leaves the orb, full width once it has spread.</summary>
        public static float BandFor(float radius)
        {
            return GameConfig.PitWarpRingBandPx * MathHelper.Clamp(0.25f + radius / 320f, 0.25f, 1f);
        }

        /// <inheritdoc />
        public override void Render(Scene scene)
        {
            if (_alpha <= 0f || _radius <= 0.5f)
                return;
            var cam = Camera ?? scene.Camera;
            if (cam == null)
                return;
            _profile ??= CreateProfile();

            BeginRender(cam);
            var batcher = Graphics.Instance.Batcher;
            var color = GameConfig.PitWarpRingColor;
            var core = Color.Lerp(color, Color.White, 0.6f);
            float band = BandFor(_radius);
            DrawBand(batcher, band, new Color(color.R, color.G, color.B, (byte)(255 * MathHelper.Clamp(_alpha * GlowStrength, 0f, 1f))));
            DrawBand(batcher, band * CoreBandScale, new Color(core.R, core.G, core.B, (byte)(255 * MathHelper.Clamp(_alpha, 0f, 1f))));
            EndRender();
        }

        private void DrawBand(Batcher batcher, float band, Color color)
        {
            int segments = (int)(MathHelper.TwoPi * _radius / SegmentLengthPx);
            segments = Math.Clamp(segments, MinSegments, MaxSegments);
            float step = MathHelper.TwoPi / segments;
            var center = new Vector2(_x, _y);
            var origin = new Vector2(0.5f, ProfileHeight * 0.5f);
            var scale = new Vector2(2f * _radius * MathF.Sin(step * 0.5f) + 1f, band / ProfileHeight); // +1 px closes the seams
            for (int i = 0; i < segments; i++)
            {
                float mid = (i + 0.5f) * step;
                // Chord midpoint, with the band centred on the circle rather than inside it
                var pos = center + new Vector2(MathF.Cos(mid), MathF.Sin(mid)) * _radius;
                batcher.Draw(_profile, pos, null, color, mid + MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>A 1×32 white strip whose alpha is a soft bump: the cross-section of the ring's band.</summary>
        private static Texture2D CreateProfile()
        {
            var data = new Color[ProfileHeight];
            for (int y = 0; y < ProfileHeight; y++)
            {
                float d = (y + 0.5f - ProfileHeight * 0.5f) / (ProfileHeight * 0.5f); // -1..1
                float a = MathF.Exp(-d * d * 4f) * (1f - d * d);
                data[y] = Color.White * MathHelper.Clamp(a, 0f, 1f);
            }
            var texture = new Texture2D(Core.GraphicsDevice, 1, ProfileHeight);
            texture.SetData(data);
            return texture;
        }

        /// <inheritdoc />
        public override void Unload()
        {
            _profile?.Dispose();
            _profile = null;
            base.Unload();
        }
    }
}
