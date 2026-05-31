using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Composites multiple HeroAnimationComponent paperdoll layers into a single 32×46 render
    /// target at RenderLayerActors. Prevents z-fighting where individual layers could appear on
    /// different sides of world objects.
    ///
    /// Usage: construct with layers in back-to-front draw order (hand2 → body → … → hand1),
    /// then add to the entity. The component marks each layer OwnedByComposite so it skips
    /// direct rendering.
    /// </summary>
    public class MultiSpriteAnimator : SpriteCompositorBase
    {
        public const int RT_WIDTH  = 32;
        public const int RT_HEIGHT = 46;

        // Entity world-position maps to RT pixel (16, 39) — sprite top sits flush at RT y=0.
        private static readonly Vector2 RtEntityPivot = new Vector2(RT_WIDTH / 2f, 39f);

        private readonly List<HeroAnimationComponent> _layers;

        /// <param name="layers">Paperdoll layers in back-to-front draw order.</param>
        public MultiSpriteAnimator(params HeroAnimationComponent[] layers)
        {
            _layers = new List<HeroAnimationComponent>(layers);
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
                batcher.Draw(sprite, drawPos, layer.Color, 0f, sprite.Origin, Vector2.One, effects, 0f);
            }
        }

        // ── Proxy helpers used by HeroJumpComponent and similar ──

        public void PlayJumpAnimation(Direction direction)
        {
            foreach (var layer in _layers)
                layer?.PlayJumpAnimation(direction);
        }

        public void UpdateAnimationForDirection(Direction direction)
        {
            foreach (var layer in _layers)
                layer?.UpdateAnimationForDirection(direction);
        }

        public new void SetColor(Color color)
        {
            foreach (var layer in _layers)
                if (layer != null) layer.Color = color;
        }
    }
}
