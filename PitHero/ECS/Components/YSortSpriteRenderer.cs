using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    public class YSortSpriteRenderer : SpriteRenderer, IYSortOffset
    {
        /// <summary>
        /// Pixels added to the entity's Y before Y-sort depth is computed, so the sort point can be
        /// placed at the sprite's ground / front-face line instead of its centre. Default 0 (sort by
        /// entity position, matching pit walls and the hero statue).
        /// </summary>
        public float YSortOffset { get; set; }

        public YSortSpriteRenderer() : base() { }
        public YSortSpriteRenderer(Sprite sprite) : base(sprite) { }
        public YSortSpriteRenderer(Texture2D texture) : base(texture) { }
    }
}
