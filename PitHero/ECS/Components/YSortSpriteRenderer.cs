using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    public class YSortSpriteRenderer : SpriteRenderer
    {
        public YSortSpriteRenderer() : base() { }
        public YSortSpriteRenderer(Sprite sprite) : base(sprite) { }
        public YSortSpriteRenderer(Texture2D texture) : base(texture) { }
    }
}
