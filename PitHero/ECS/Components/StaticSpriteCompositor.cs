using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Composites a fixed set of SpriteRenderer layers into a single render target displayed
    /// on one render layer.  Unlike MultiSpriteAnimator, children need not be HeroAnimationComponents
    /// and require no animation update — they are simply sampled each frame.
    ///
    /// The child SpriteRenderers are disabled (preventing direct rendering) but remain alive
    /// so callers can update their Sprite / Color properties normally; changes are picked up
    /// automatically the next time the RT is composited.
    ///
    /// Usage:
    ///   var compositor = entity.AddComponent(
    ///       new StaticSpriteCompositor(
    ///           new[] { baseRenderer, woodRenderer },    // back → front
    ///           rtWidth: 32, rtHeight: 32,
    ///           rtEntityPivot: new Vector2(16, 16)));    // where entity pos maps in the RT
    ///   compositor.SetRenderLayer(GameConfig.RenderLayerTreasureComposite);
    /// </summary>
    public class StaticSpriteCompositor : RenderableComponent, IUpdatable
    {
        private readonly SpriteRenderer[] _layers;
        private readonly int              _rtWidth;
        private readonly int              _rtHeight;
        private readonly Vector2          _rtEntityPivot;

        private CompositingRenderer _renderer;
        private Sprite              _compositeSprite;

        public override RectangleF Bounds
        {
            get
            {
                if (_areBoundsDirty)
                {
                    _bounds.CalculateBounds(
                        Entity.Transform.Position, _localOffset, _rtEntityPivot,
                        Entity.Transform.Scale, Entity.Transform.Rotation,
                        _rtWidth, _rtHeight);
                    _areBoundsDirty = false;
                }
                return _bounds;
            }
        }

        /// <param name="layers">Child renderers in back-to-front draw order.</param>
        /// <param name="rtWidth">Render target width in pixels.</param>
        /// <param name="rtHeight">Render target height in pixels.</param>
        /// <param name="rtEntityPivot">
        ///   Pixel inside the RT that corresponds to the entity world-position.
        ///   For a center-origin sprite with no local offset: (rtWidth/2, rtHeight/2).
        /// </param>
        public StaticSpriteCompositor(IList<SpriteRenderer> layers, int rtWidth, int rtHeight, Vector2 rtEntityPivot)
        {
            _layers        = new SpriteRenderer[layers.Count];
            layers.CopyTo(_layers, 0);
            _rtWidth       = rtWidth;
            _rtHeight      = rtHeight;
            _rtEntityPivot = rtEntityPivot;
        }

        public override void OnAddedToEntity()
        {
            // Suppress direct rendering of child layers — we own the draw
            foreach (var layer in _layers)
                layer?.SetEnabled(false);

            _renderer = new CompositingRenderer(_layers, _rtWidth, _rtHeight);

            _compositeSprite = new Sprite(_renderer.RenderTexture.RenderTarget);
            _compositeSprite.Origin = _rtEntityPivot;
        }

        public override void OnRemovedFromEntity()
        {
            _renderer?.RenderTexture?.Dispose();
            _renderer = null;
        }

        public void Update() { }

        public override void Render(Batcher batcher, Camera camera)
        {
            if (_renderer == null || _compositeSprite == null)
                return;

            // Round to integer pixels — prevents sub-pixel shimmer when the entity moves
            var entityPos = new Vector2(
                (float)Math.Round(Entity.Transform.Position.X),
                (float)Math.Round(Entity.Transform.Position.Y));

            var prevRTs = Core.GraphicsDevice.GetRenderTargets();
            batcher.End();

            _renderer.RenderComposite(entityPos, _rtEntityPivot);

            Core.GraphicsDevice.SetRenderTargets(prevRTs.Length > 0 ? prevRTs : null);

            batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise,
                null, camera.TransformMatrix, false);

            batcher.Draw(
                _compositeSprite,
                entityPos + _localOffset,
                Color,
                Entity.Transform.Rotation,
                _compositeSprite.Origin,
                Entity.Transform.Scale,
                SpriteEffects.None,
                _layerDepth);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private renderer — never added to the scene
        // ─────────────────────────────────────────────────────────────────────

        private sealed class CompositingRenderer : Renderer
        {
            private readonly SpriteRenderer[] _layers;
            private Matrix _rtTransform;

            public CompositingRenderer(SpriteRenderer[] layers, int rtWidth, int rtHeight) : base(0)
            {
                _layers = layers;
                RenderTexture = new RenderTexture(rtWidth, rtHeight, SurfaceFormat.Color, DepthFormat.None);
                RenderTexture.ResizeBehavior = RenderTexture.RenderTextureResizeBehavior.None;
                RenderTargetClearColor = Color.Transparent;
            }

            protected override void BeginRender(Camera cam)
            {
                Core.GraphicsDevice.SetRenderTarget(RenderTexture);
                Core.GraphicsDevice.Clear(RenderTargetClearColor);
                _currentMaterial = Material;
                Graphics.Instance.Batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise,
                    null, _rtTransform, false);
            }

            public void RenderComposite(Vector2 entityPos, Vector2 rtPivot)
            {
                _rtTransform = Matrix.CreateTranslation(
                    rtPivot.X - entityPos.X,
                    rtPivot.Y - entityPos.Y,
                    0f);

                BeginRender(null); // camera param unused in our override

                var batcher = Graphics.Instance.Batcher;
                foreach (var layer in _layers)
                {
                    // SpriteRenderer.Render draws Sprite at Entity.Transform.Position + LocalOffset
                    // using the batcher's translation matrix to map world coords → RT coords.
                    // Camera is unused by SpriteRenderer so null is safe.
                    if (layer?.Sprite != null)
                        layer.Render(batcher, null);
                }

                EndRender();
            }

            public override void Render(Scene scene) { }
        }
    }
}
