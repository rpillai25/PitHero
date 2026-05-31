using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Composites multiple HeroAnimationComponent paperdoll layers into a single 32x46 render
    /// target displayed at RenderLayerActors.  This prevents z-fighting where individual
    /// layers could appear on different sides of world objects (e.g. one body part in front
    /// of a monster, another behind it).
    ///
    /// Usage: add all HeroAnimationComponent sub-components to the entity first, then add
    /// MultiSpriteAnimator.  It discovers them by type, marks them so they suppress direct
    /// rendering, and composites them itself each frame.
    /// </summary>
    public class MultiSpriteAnimator : RenderableComponent, IUpdatable
    {
        public const int RT_WIDTH  = 32;
        public const int RT_HEIGHT = 46;

        // Where the entity world-position maps to inside the render target.
        // Sprite origin from atlas = (0.5, 0.5) → pixel (16, 23) for a 32×46 sprite.
        // localOffset = (0, −16).  Draw-anchor lands at RT y = RtPivot.Y − 16.
        // Sprite top in RT = (RtPivot.Y − 16) − 23 = RtPivot.Y − 39.
        // For sprite top at RT y=0: RtPivot.Y = 39.  Horizontal center = 16.
        private static readonly Vector2 RtEntityPivot = new Vector2(RT_WIDTH / 2f, 39f);

        private PaperdollRenderer _renderer;
        private Sprite            _compositeSprite;
        private int               _lastYSortRow = int.MinValue;

        public override RectangleF Bounds
        {
            get
            {
                if (_areBoundsDirty)
                {
                    _bounds.CalculateBounds(
                        Entity.Transform.Position, _localOffset, RtEntityPivot,
                        Entity.Transform.Scale, Entity.Transform.Rotation,
                        RT_WIDTH, RT_HEIGHT);
                    _areBoundsDirty = false;
                }
                return _bounds;
            }
        }

        public override void OnAddedToEntity()
        {
            // Collect layers in back-to-front draw order
            var layers = new List<HeroAnimationComponent>
            {
                Entity.GetComponent<HeroHand2AnimationComponent>(),
                Entity.GetComponent<HeroBodyAnimationComponent>(),
                Entity.GetComponent<HeroPantsAnimationComponent>(),
                Entity.GetComponent<HeroShirtAnimationComponent>(),
                Entity.GetComponent<HeroHeadAnimationComponent>(),
                Entity.GetComponent<HeroEyesAnimationComponent>(),
                Entity.GetComponent<HeroHairAnimationComponent>(),
                Entity.GetComponent<HeroHand1AnimationComponent>(),
            };
            layers.RemoveAll(l => l == null);

            // Suppress direct rendering — MultiSpriteAnimator owns the draw
            foreach (var layer in layers)
                layer.OwnedByComposite = true;

            _renderer = new PaperdollRenderer(layers);

            _compositeSprite = new Sprite(_renderer.RenderTexture.RenderTarget);
            _compositeSprite.Origin = RtEntityPivot;
        }

        public override void OnRemovedFromEntity()
        {
            _renderer?.RenderTexture?.Dispose();
            _renderer = null;
        }

        public void Update()
        {
            // Snap to tile row so depth is stable while crossing a tile; only re-sort when the row changes.
            // This prevents a sort every frame that would cause flickering when two entities share a row.
            var row = (int)(Entity.Transform.Position.Y / GameConfig.TileSize);
            if (row != _lastYSortRow)
            {
                _lastYSortRow = row;
                SetLayerDepth(Mathf.Clamp01(1f - row * GameConfig.TileSize * GameConfig.YSortDepthScale));
            }
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            if (_renderer == null || _compositeSprite == null)
                return;

            // Round to integer pixels so the RT content aligns exactly with the display position
            // each frame — prevents the sub-pixel shimmer ("heat haze") that occurs when the entity
            // moves at fractional pixel offsets.
            var entityPos = new Vector2(
                (float)Math.Round(Entity.Transform.Position.X),
                (float)Math.Round(Entity.Transform.Position.Y));

            var prevRTs = Core.GraphicsDevice.GetRenderTargets();
            batcher.End();

            _renderer.RenderComposite(entityPos);

            Core.GraphicsDevice.SetRenderTargets(prevRTs.Length > 0 ? prevRTs : null);

            // Resume with explicit point sampling so the RT composite is not filtered
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

        // ── Proxy helpers used by HeroJumpComponent, HeroDeathComponent, etc. ──

        public void PlayJumpAnimation(Direction direction)
        {
            if (_renderer == null) return;
            foreach (var layer in _renderer.Layers)
                layer?.PlayJumpAnimation(direction);
        }

        public void UpdateAnimationForDirection(Direction direction)
        {
            if (_renderer == null) return;
            foreach (var layer in _renderer.Layers)
                layer?.UpdateAnimationForDirection(direction);
        }

        public new void SetColor(Color color)
        {
            if (_renderer == null) return;
            foreach (var layer in _renderer.Layers)
                if (layer != null) layer.Color = color;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Private renderer — never added to the scene; called directly from Render()
        // ────────────────────────────────────────────────────────────────────────

        private sealed class PaperdollRenderer : Renderer
        {
            public readonly List<HeroAnimationComponent> Layers;
            private Matrix _rtTransform;

            public PaperdollRenderer(List<HeroAnimationComponent> layers) : base(0)
            {
                Layers = layers;
                RenderTexture = new RenderTexture(RT_WIDTH, RT_HEIGHT, SurfaceFormat.Color, DepthFormat.None);
                RenderTexture.ResizeBehavior = RenderTexture.RenderTextureResizeBehavior.None;
                RenderTargetClearColor = Color.Transparent;
            }

            // Override to use our translation matrix instead of camera transform,
            // and to clear to transparent on every composite pass.
            protected override void BeginRender(Camera cam)
            {
                Core.GraphicsDevice.SetRenderTarget(RenderTexture);
                Core.GraphicsDevice.Clear(RenderTargetClearColor);
                _currentMaterial = Material;
                // Use point sampling inside the RT to match atlas sprite rendering quality
                Graphics.Instance.Batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise,
                    null, _rtTransform, false);
            }

            public void RenderComposite(Vector2 entityPos)
            {
                // Build a pure translation that maps entity world-position → RT pivot pixel
                _rtTransform = Matrix.CreateTranslation(
                    RtEntityPivot.X - entityPos.X,
                    RtEntityPivot.Y - entityPos.Y,
                    0f);

                BeginRender(null); // cam param unused in our override

                var batcher = Graphics.Instance.Batcher;
                foreach (var layer in Layers)
                {
                    if (layer?.Sprite == null) continue;
                    var sprite  = layer.Sprite;
                    var drawPos = entityPos + layer.LocalOffset;
                    var effects = layer.FlipX
                        ? SpriteEffects.FlipHorizontally
                        : SpriteEffects.None;

                    batcher.Draw(sprite, drawPos, layer.Color, 0f,
                        sprite.Origin, Vector2.One, effects, 0f);
                }

                EndRender();
            }

            // Not used — we never add this renderer to the scene
            public override void Render(Scene scene) { }
        }
    }
}
