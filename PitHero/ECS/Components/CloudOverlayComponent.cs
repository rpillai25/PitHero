using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.Rendering;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Draws the volumetric scrolling cloud overlay (#see docs/RenderingSystem.md) as a single quad
    /// covering the camera's world-space bounds every frame. The quad is textured with the controller's
    /// generated noise texture using a null source rect, which gives the vertex shader UVs spanning
    /// 0..1 across the viewport; CloudOverlay.fx reconstructs per-pixel world position from
    /// <c>CameraTopLeft</c>/<c>CameraSize</c> uniforms set here, so a small quad renders world-anchored
    /// clouds under any pan/zoom without needing a giant fixed-size quad.
    /// </summary>
    public class CloudOverlayComponent : RenderableComponent
    {
        // A couple pixels of slack against edge seams when the camera bounds are recomputed mid-frame.
        const float EdgeInflatePx = 2f;

        // Fixed huge rect (mirrors TreeBandComponent's Bounds override) — camera culling is bypassed
        // entirely via IsVisibleFromCamera below, so this only needs to be sane for debug rendering.
        static readonly RectangleF HugeBounds = new RectangleF(-100000f, -100000f, 200000f, 200000f);

        readonly CloudOverlayController _controller;

        public CloudOverlayComponent(CloudOverlayController controller)
        {
            _controller = controller;
        }

        public override RectangleF Bounds => HugeBounds;

        /// <summary>Clouds always cover the full camera view, so skip the normal bounds-intersection cull.</summary>
        public override bool IsVisibleFromCamera(Camera camera) => true;

        public override void Render(Batcher batcher, Camera camera)
        {
            if (_controller?.Material?.Effect is not CloudOverlayEffect effect || _controller.NoiseTexture == null)
                return;

            var b = camera.Bounds;
            b.X -= EdgeInflatePx;
            b.Y -= EdgeInflatePx;
            b.Width += EdgeInflatePx * 2f;
            b.Height += EdgeInflatePx * 2f;

            effect.CameraTopLeft = b.Location;
            effect.CameraSize = new Vector2(b.Width, b.Height);

            var tex = _controller.NoiseTexture;
            var scale = new Vector2(b.Width / tex.Width, b.Height / tex.Height);
            batcher.Draw(tex, b.Location, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
