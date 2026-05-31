using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Composites multiple ICompositeLayer layers into a single 32×46 render target at
    /// RenderLayerActors. Prevents z-fighting where individual layers could appear on different
    /// sides of world objects.
    ///
    /// Works with any ICompositeLayer implementation — hero paperdoll, dragon body parts, etc.
    /// Each layer is responsible for honouring OwnedByComposite in IsVisibleFromCamera so the
    /// DefaultRenderer skips it.
    ///
    /// Usage: construct with layers in back-to-front draw order, then add to the entity.
    /// </summary>
    public class MultiSpriteAnimator : SpriteCompositorBase
    {
        public const int RT_WIDTH  = 32;
        public const int RT_HEIGHT = 46;

        // Entity world-position maps to RT pixel (16, 39) — sprite top sits flush at RT y=0.
        private static readonly Vector2 RtEntityPivot = new Vector2(RT_WIDTH / 2f, 39f);

        private readonly List<ICompositeLayer> _layers;

        /// <param name="layers">Layers in back-to-front draw order.</param>
        public MultiSpriteAnimator(params ICompositeLayer[] layers)
        {
            _layers = new List<ICompositeLayer>(layers);
        }

        public override void OnAddedToEntity()
        {
            foreach (var layer in _layers)
                if (layer != null) layer.OwnedByComposite = true;

            InitCompositor(RT_WIDTH, RT_HEIGHT, RtEntityPivot, DrawLayers);
        }

        private void DrawLayers(Batcher batcher, Vector2 entityPos)
        {
            foreach (var layer in _layers)
            {
                if (layer?.Sprite == null) continue;
                var sprite  = layer.Sprite;
                var drawPos = entityPos + layer.LocalOffset;
                var effects = layer.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                batcher.Draw(sprite, drawPos, layer.LayerColor, 0f, sprite.Origin, Vector2.One, effects, 0f);
            }
        }
    }
}
