using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A SpriteRenderer that automatically Y-sorts within RenderLayerActors by updating
    /// LayerDepth whenever the entity crosses a tile-row boundary.
    ///
    /// Use this in place of plain SpriteRenderer for any single-sprite world entity that
    /// lives at RenderLayerActors (walls, orbs, statues, etc.).  The tile-row snap ensures
    /// the sort only triggers at row boundaries, not on every pixel of smooth movement,
    /// preventing the constant re-sorts that cause flickering between nearby entities.
    /// </summary>
    public class YSortSpriteRenderer : SpriteRenderer, IUpdatable
    {
        private int _lastYSortRow = int.MinValue;

        public YSortSpriteRenderer() : base() { }
        public YSortSpriteRenderer(Sprite sprite) : base(sprite) { }
        public YSortSpriteRenderer(Texture2D texture) : base(texture) { }

        public void Update()
        {
            var row = (int)(Entity.Transform.Position.Y / GameConfig.TileSize);
            if (row != _lastYSortRow)
            {
                _lastYSortRow = row;
                SetLayerDepth(Mathf.Clamp01(1f - row * GameConfig.TileSize * GameConfig.YSortDepthScale));
            }
        }
    }
}
