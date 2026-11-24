using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;
using PitHero.Rendering;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Draws a stack of ICompositeLayer layers as one 32×48 image at RenderLayerActors, so the layers of
    /// overlapping characters never interleave.
    ///
    /// Each distinct pose (every layer's sprite, offset, flip and tint) is baked once into a shared
    /// <see cref="CharacterFrameAtlas"/> page and then drawn as a single quad from that page, so characters
    /// batch with each other instead of costing two render-target switches each per frame (issue #459).
    /// A pose that cannot be baked falls back to the per-frame render target in <see cref="SpriteCompositorBase"/>.
    ///
    /// The layer animators keep ticking (the pose is read from them every frame) and stay the source for
    /// replay capture; each layer honours OwnedByComposite in IsVisibleFromCamera so the DefaultRenderer
    /// skips it. Usage: construct with layers in back-to-front draw order, then add to the entity.
    /// </summary>
    public class MultiSpriteAnimator : SpriteCompositorBase
    {
        public const int RT_WIDTH  = GameConfig.CharacterFrameCellWidth;
        public const int RT_HEIGHT = GameConfig.CharacterFrameCellHeight;

        // Entity world-position maps to RT pixel (16, 39) — sprite top sits flush at RT y=0.
        private static readonly Vector2 RtEntityPivot = new Vector2(RT_WIDTH / 2f, 39f);

        private readonly List<ICompositeLayer> _layers;
        private CharacterPoseCache _poses;
        private readonly Sprite[] _poseSprites = new Sprite[CharacterPoseCache.MaxLayers];
        private readonly Vector2[] _poseOffsets = new Vector2[CharacterPoseCache.MaxLayers];
        private readonly bool[] _poseFlips = new bool[CharacterPoseCache.MaxLayers];
        private readonly Color[] _poseColors = new Color[CharacterPoseCache.MaxLayers];

        /// <summary>Number of composited layers (read by the replay frame census).</summary>
        public int LayerCount => _layers.Count;

        /// <summary>Layer at <paramref name="index"/>, back to front.</summary>
        public ICompositeLayer GetLayer(int index) => _layers[index];

        /// <summary>Entity position (whole pixels) plus <see cref="RenderableComponent.LocalOffset"/>: where the body is drawn.</summary>
        public Vector2 DrawPosition => SnappedEntityPosition + _localOffset;

        /// <param name="layers">Layers in back-to-front draw order; null entries are skipped.</param>
        public MultiSpriteAnimator(params ICompositeLayer[] layers)
        {
            _layers = new List<ICompositeLayer>(layers.Length);
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null) _layers.Add(layers[i]);
            }
        }

        public override void OnAddedToEntity()
        {
            for (int i = 0; i < _layers.Count; i++)
                _layers[i].OwnedByComposite = true;

            _poses = new CharacterPoseCache(RtEntityPivot);
            InitCompositor(RT_WIDTH, RT_HEIGHT, RtEntityPivot, DrawLayers);
        }

        public override void OnRemovedFromEntity()
        {
            _poses?.ReleaseAll();
            base.OnRemovedFromEntity();
        }

        /// <summary>
        /// Returns the baked atlas sprite for a pose given as parallel arrays (from <paramref name="start"/>, for
        /// <paramref name="count"/> layers), baking it into this character's cache if needed. Must be called while
        /// <paramref name="batcher"/> is drawing with <paramref name="camera"/>. Returns null when the pose can't be
        /// baked. Used by <see cref="AfterimageTrailComponent"/> for flat trail copies.
        /// </summary>
        public Sprite ResolvePose(Batcher batcher, Camera camera, Sprite[] sprites, Vector2[] offsets, bool[] flips,
            Color[] colors, int start, int count)
        {
            return _poses?.Resolve(batcher, camera, sprites, offsets, flips, colors, start, count);
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            int count = 0;
            bool fits = true;
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                var sprite = layer.Sprite;
                if (sprite == null)
                    continue;
                if (count == CharacterPoseCache.MaxLayers)
                {
                    fits = false;
                    break;
                }
                _poseSprites[count] = sprite;
                _poseOffsets[count] = layer.LocalOffset;
                _poseFlips[count] = layer.FlipX;
                _poseColors[count] = layer.LayerColor;
                count++;
            }
            if (count == 0)
                return;

            if (fits && GameConfig.BakeCharacterFrames)
            {
                var baked = ResolvePose(batcher, camera, _poseSprites, _poseOffsets, _poseFlips, _poseColors, 0, count);
                if (baked != null)
                {
                    RenderStats.BakedDraws++;
                    batcher.Draw(baked, DrawPosition, Color, Entity.Transform.Rotation, baked.Origin,
                        Entity.Transform.Scale, SpriteEffects.None, _layerDepth);
                    return;
                }
            }

            RenderStats.FallbackDraws++;
            base.Render(batcher, camera);
        }

        private void DrawLayers(Batcher batcher, Vector2 entityPos)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.Sprite == null) continue;
                var sprite  = layer.Sprite;
                var drawPos = entityPos + layer.LocalOffset;
                var effects = layer.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                batcher.Draw(sprite, drawPos, layer.LayerColor, 0f, sprite.Origin, Vector2.One, effects, 0f);
            }
        }
    }
}
