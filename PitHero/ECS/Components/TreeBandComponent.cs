using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A decorative band of deterministic random trees filling the empty space north or south of the
    /// map (#348). The whole band is painted once into its own RenderTexture — trees are drawn
    /// top-to-bottom like a painter, so lower trees overdraw higher ones — and every frame after that
    /// only blits that single texture, instead of redrawing ~1900 sprites.
    ///
    /// The render texture covers the band rect plus TreeBandMapOverlapPx on the map-facing side, so
    /// trees may spill slightly over the map edge for an organic seam while anything beyond that is
    /// clipped for free by the texture bounds.
    /// </summary>
    public class TreeBandComponent : RenderableComponent
    {
        private readonly Sprite _tree;
        private readonly Sprite _tree2;
        private readonly int _startTileY;
        private readonly int _endTileY;
        private readonly int _seed;
        private readonly int _rtWidth;
        private readonly int _rtHeight;
        private readonly float _rtWorldTop;

        private RenderTexture _renderTexture;
        private Sprite _bandSprite;
        private bool _needsPaint = true;

        /// <param name="tree">The shorter tree sprite (center origin).</param>
        /// <param name="tree2">The taller tree sprite (center origin).</param>
        /// <param name="mapWidthPx">Full map width in pixels — the band spans all of it.</param>
        /// <param name="startTileY">First tile row of the band (inclusive, may be negative).</param>
        /// <param name="endTileY">Last tile row of the band (inclusive).</param>
        /// <param name="seed">Seed for this band's local deterministic RNG.</param>
        /// <param name="overlapBelow">
        ///   True for the top band (trees spill downward into the map), false for the bottom band
        ///   (trees spill upward into the map).
        /// </param>
        public TreeBandComponent(Sprite tree, Sprite tree2, int mapWidthPx,
                                 int startTileY, int endTileY, int seed, bool overlapBelow)
        {
            _tree = tree;
            _tree2 = tree2;
            _startTileY = startTileY;
            _endTileY = endTileY;
            _seed = seed;

            var bandTop = startTileY * GameConfig.TileSize;
            var rows = endTileY - startTileY + 1;

            _rtWidth = mapWidthPx;
            _rtHeight = rows * GameConfig.TileSize + GameConfig.TreeBandMapOverlapPx;
            // The overlap is added on the map-facing side: below for the top band (texture keeps its
            // top edge), above for the bottom band (texture top moves up into the map).
            _rtWorldTop = overlapBelow ? bandTop : bandTop - GameConfig.TreeBandMapOverlapPx;
        }

        /// <summary>Band rect in world space. Overridden so camera culling uses the real extents.</summary>
        public override RectangleF Bounds
        {
            get
            {
                if (_areBoundsDirty)
                {
                    _bounds.X = Entity.Transform.Position.X + _localOffset.X;
                    _bounds.Y = Entity.Transform.Position.Y + _localOffset.Y;
                    _bounds.Width = _rtWidth;
                    _bounds.Height = _rtHeight;
                    _areBoundsDirty = false;
                }
                return _bounds;
            }
        }

        public override void OnAddedToEntity()
        {
            SetLocalOffset(new Vector2(0f, _rtWorldTop));

            _renderTexture = new RenderTexture(_rtWidth, _rtHeight, SurfaceFormat.Color, DepthFormat.None);
            _renderTexture.ResizeBehavior = RenderTexture.RenderTextureResizeBehavior.None;

            _bandSprite = new Sprite(_renderTexture.RenderTarget);
            _bandSprite.Origin = Vector2.Zero;
        }

        public override void OnRemovedFromEntity()
        {
            _renderTexture?.Dispose();
            _renderTexture = null;
            _bandSprite = null;
        }

        /// <summary>
        /// Forces one Render call before the band has been painted. Both bands sit entirely outside
        /// the viewport at the default 1x zoom, so the normal camera cull would defer the one-time
        /// paint until the player first zooms out — a visible hitch. The extra off-screen quad drawn
        /// on frame one is free (the GPU clips it).
        /// </summary>
        public override bool IsVisibleFromCamera(Camera camera)
        {
            if (_needsPaint)
                return true;
            return base.IsVisibleFromCamera(camera);
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            if (_bandSprite == null)
                return;

            if (_needsPaint)
            {
                PaintBandOnce(batcher, camera);
                _needsPaint = false;
            }

            batcher.Draw(_bandSprite, Entity.Transform.Position + _localOffset, Color,
                0f, Vector2.Zero, Vector2.One, SpriteEffects.None, _layerDepth);
        }

        /// <summary>
        /// Renders every tree into the band's RenderTexture a single time. Follows the render-target
        /// save/restore sequence documented in docs/RenderingSystem.md: flush the outer batch, swap
        /// render targets, paint, then restore the scene target and resume the outer batch.
        /// </summary>
        private void PaintBandOnce(Batcher batcher, Camera camera)
        {
            var prevRTs = Core.GraphicsDevice.GetRenderTargets();
            batcher.End();

            Core.GraphicsDevice.SetRenderTarget(_renderTexture);
            Core.GraphicsDevice.Clear(Color.Transparent);

            // World -> render-texture-local, so PaintTrees can work in plain world coordinates.
            var rtTransform = Matrix.CreateTranslation(0f, -_rtWorldTop, 0f);
            batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, null, rtTransform, false);
            PaintTrees(batcher);
            batcher.End();

            Core.GraphicsDevice.SetRenderTargets(prevRTs.Length > 0 ? prevRTs : null);
            // Resume with our own effect bound. The renderer still believes the grading material is
            // active and so will not re-Begin for us; passing null here would drop grading from this
            // band AND from the Base tilemap that shares the material later in the same batch.
            batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, Material?.Effect, camera.TransformMatrix, false);

            Debug.Log("[TreeBand] Painted band rows {0}..{1} into a {2}x{3} render texture",
                _startTileY, _endTileY, _rtWidth, _rtHeight);
        }

        /// <summary>
        /// Paints the band row by row, top to bottom. Uses a local System.Random seeded from a fixed
        /// key so the layout is identical every run without touching the global Nez.Random stream
        /// (whose call order is a combat save/replay contract).
        /// </summary>
        private void PaintTrees(Batcher batcher)
        {
            var rng = new System.Random(_seed);

            for (int row = _startTileY; row <= _endTileY; row++)
            {
                // Trunks sit on the row's bottom edge, jittered per tree.
                var rowBaseY = row * GameConfig.TileSize + GameConfig.TileSize;

                // Start off the left edge by a random amount and stagger each row so rows never line up.
                var x = -rng.Next(0, GameConfig.TreeBandBaseSpacingPx)
                      + rng.Next(-GameConfig.TreeBandRowXOffsetPx, GameConfig.TreeBandRowXOffsetPx + 1);

                while (x < _rtWidth + GameConfig.TreeBandBaseSpacingPx)
                {
                    var sprite = rng.NextDouble() < GameConfig.TreeBandTree2Chance ? _tree2 : _tree;
                    var effects = rng.NextDouble() < GameConfig.TreeBandFlipChance
                        ? SpriteEffects.FlipHorizontally
                        : SpriteEffects.None;
                    var baseY = rowBaseY + rng.Next(-GameConfig.TreeBandRowYJitterPx,
                                                     GameConfig.TreeBandRowYJitterPx + 1);

                    // Sprites are center-origin, so lift by half the height to put the trunk base on baseY.
                    var pos = new Vector2(x, baseY - sprite.SourceRect.Height * 0.5f);
                    batcher.Draw(sprite, pos, Color.White, 0f, sprite.Origin, Vector2.One, effects, 0f);

                    x += GameConfig.TreeBandBaseSpacingPx
                       + rng.Next(-GameConfig.TreeBandSpacingJitterPx,
                                   GameConfig.TreeBandSpacingJitterPx + 1);
                }
            }
        }
    }
}
