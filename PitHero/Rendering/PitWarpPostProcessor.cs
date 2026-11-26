using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    /// <summary>
    /// The pit twist of the wizard-orb warp (issue #469): a scene post-processor that swirls the pit
    /// region of the scene render target into a drain. Disabled (zero cost) whenever no warp is showing.
    /// The presentation pass feeds it once per rendered frame with <see cref="SetState"/>, from the live
    /// <see cref="Services.PitWarpService"/> or, while a recorded replay frame is on screen, from the
    /// frame's HUD record. The floating party renders after post-processing
    /// (<see cref="PitWarpPartyRenderer"/>), so it is never twisted.
    /// </summary>
    public sealed class PitWarpPostProcessor : PostProcessor
    {
        /// <summary>Runs before any other post-processor.</summary>
        public const int Order = 0;

        private PitWarpEffect _effect;
        private float _twist, _pull, _spin;
        private int _regionX, _regionY, _regionWidth, _regionHeight;

        public PitWarpPostProcessor() : base(Order)
        {
            Enabled = false;
        }

        /// <inheritdoc />
        public override void OnAddedToScene(Scene scene)
        {
            base.OnAddedToScene(scene);
            _effect = new PitWarpEffect();
            Effect = _effect;
        }

        /// <summary>
        /// Sets the warp shown this frame. A twist and pull of zero (no warp) disables the pass.
        /// The region is the swirling pit area in world pixels.
        /// </summary>
        public void SetState(float twist, float pull, float spin, int regionX, int regionY, int regionWidth, int regionHeight)
        {
            _twist = twist;
            _pull = pull;
            _spin = spin;
            _regionX = regionX;
            _regionY = regionY;
            _regionWidth = regionWidth;
            _regionHeight = regionHeight;
            Enabled = (twist != 0f || pull != 0f) && regionWidth > 0 && regionHeight > 0;
        }

        /// <inheritdoc />
        public override void Process(RenderTarget2D source, RenderTarget2D destination)
        {
            var cam = _scene?.Camera;
            if (cam == null || _effect == null)
            {
                DrawFullscreenQuad(source, destination, null);
                return;
            }

            // World region -> render-target UV through the live camera (the player may pan or zoom mid-warp)
            var topLeft = cam.WorldToScreenPoint(new Vector2(_regionX, _regionY));
            var bottomRight = cam.WorldToScreenPoint(new Vector2(_regionX + _regionWidth, _regionY + _regionHeight));
            float w = source.Width, h = source.Height;
            var region = new Vector4(
                (topLeft.X + bottomRight.X) * 0.5f / w,
                (topLeft.Y + bottomRight.Y) * 0.5f / h,
                (bottomRight.X - topLeft.X) * 0.5f / w,
                (bottomRight.Y - topLeft.Y) * 0.5f / h);
            _effect.Set(region, _twist, _pull, _spin);
            DrawFullscreenQuad(source, destination, _effect);
        }

        /// <inheritdoc />
        public override void Unload()
        {
            Effect = null; // our own effect: not owned by the scene's content manager
            _effect?.Dispose();
            _effect = null;
            base.Unload();
        }
    }
}
