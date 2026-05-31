using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Composites a fixed set of SpriteRenderer layers into a single render target.
    /// Children are SetEnabled(false) so the DefaultRenderer skips them; this compositor
    /// calls each child's Render() directly each frame, so Sprite/Color changes are
    /// picked up automatically.
    ///
    /// Usage:
    ///   var compositor = entity.AddComponent(
    ///       new StaticSpriteCompositor(
    ///           new[] { baseRenderer, woodRenderer },  // back → front
    ///           rtWidth: 32, rtHeight: 32,
    ///           rtEntityPivot: new Vector2(16, 16)));
    ///   compositor.SetRenderLayer(GameConfig.RenderLayerTreasureComposite);
    /// </summary>
    public class StaticSpriteCompositor : SpriteCompositorBase
    {
        private readonly SpriteRenderer[] _layers;
        private readonly int     _w;
        private readonly int     _h;
        private readonly Vector2 _pivot;

        /// <param name="layers">Child renderers in back-to-front draw order.</param>
        /// <param name="rtWidth">Render target width in pixels.</param>
        /// <param name="rtHeight">Render target height in pixels.</param>
        /// <param name="rtEntityPivot">
        ///   Pixel inside the RT that maps to the entity world-position.
        ///   For a center-origin sprite with no local offset: (rtWidth/2, rtHeight/2).
        /// </param>
        public StaticSpriteCompositor(IList<SpriteRenderer> layers, int rtWidth, int rtHeight, Vector2 rtEntityPivot)
        {
            _layers = new SpriteRenderer[layers.Count];
            layers.CopyTo(_layers, 0);
            _w     = rtWidth;
            _h     = rtHeight;
            _pivot = rtEntityPivot;
        }

        public override void OnAddedToEntity()
        {
            foreach (var layer in _layers)
                layer?.SetEnabled(false);

            InitCompositor(_w, _h, _pivot, DrawLayers);
        }

        private void DrawLayers(Batcher batcher, Vector2 entityPos)
        {
            foreach (var layer in _layers)
                if (layer?.Sprite != null)
                    layer.Render(batcher, null);
        }
    }
}
