using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Shared base for render-target compositing components. Handles Y-sort, bounds calculation,
    /// the per-frame RT blit, and disposal. Subclasses call InitCompositor() from OnAddedToEntity()
    /// and supply a draw callback for their layer-specific rendering.
    /// </summary>
    public abstract class SpriteCompositorBase : RenderableComponent
    {
        protected Sprite  _compositeSprite;
        protected int     _rtWidth;
        protected int     _rtHeight;
        protected Vector2 _rtEntityPivot;

        private CompositorRenderer _renderer;

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

        protected void InitCompositor(int rtWidth, int rtHeight, Vector2 rtEntityPivot,
                                      Action<Batcher, Vector2> drawLayers)
        {
            _rtWidth       = rtWidth;
            _rtHeight      = rtHeight;
            _rtEntityPivot = rtEntityPivot;

            _renderer = new CompositorRenderer(rtWidth, rtHeight, drawLayers);
            _compositeSprite        = new Sprite(_renderer.RenderTexture.RenderTarget);
            _compositeSprite.Origin = rtEntityPivot;
        }

        public override void OnRemovedFromEntity()
        {
            _renderer?.RenderTexture?.Dispose();
            _renderer = null;
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            if (_renderer == null || _compositeSprite == null)
                return;

            // Round to integer pixels — prevents sub-pixel shimmer when the entity moves.
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
        // Single shared renderer — never added to the scene
        // ─────────────────────────────────────────────────────────────────────

        private sealed class CompositorRenderer : Renderer
        {
            private readonly Action<Batcher, Vector2> _drawLayers;
            private Matrix _rtTransform;

            public CompositorRenderer(int rtWidth, int rtHeight, Action<Batcher, Vector2> drawLayers) : base(0)
            {
                _drawLayers = drawLayers;
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

            public void RenderComposite(Vector2 entityPos, Vector2 pivot)
            {
                _rtTransform = Matrix.CreateTranslation(pivot.X - entityPos.X, pivot.Y - entityPos.Y, 0f);
                BeginRender(null);
                _drawLayers(Graphics.Instance.Batcher, entityPos);
                EndRender();
            }

            public override void Render(Scene scene) { }
        }
    }
}
