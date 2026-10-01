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
        /// entity position, matching pit walls and the wizard orb, whose 32px sprites already sit on
        /// their tile centre).
        /// </summary>
        public float YSortOffset { get; set; }

        public YSortSpriteRenderer() : base() { }
        public YSortSpriteRenderer(Sprite sprite) : base(sprite) { }
        public YSortSpriteRenderer(Texture2D texture) : base(texture) { }

        /// <summary>
        /// Offset that moves the sort point of a centre-origin sprite from its centre to the centre of
        /// its bottom tile row — the convention every actor already uses (a 32px sprite sits on its tile
        /// centre; medium/large monsters are shifted so their bottom tile does). Zero for 32px sprites,
        /// so applying it to a one-tile crop changes nothing. Buildings deliberately sort lower than
        /// this (their bottom edge) so a worker on the doorway row draws behind the facade.
        /// </summary>
        public static float BottomTileOffset(Sprite sprite)
        {
            if (sprite == null)
                return 0f;
            return (sprite.SourceRect.Height - GameConfig.TileSize) / 2f;
        }

        /// <summary>
        /// Sets <see cref="YSortOffset"/> from the current sprite via <see cref="BottomTileOffset"/>.
        /// Call again after swapping <see cref="SpriteRenderer.Sprite"/> for one of a different height.
        /// </summary>
        public void AnchorToBottomTile()
        {
            YSortOffset = BottomTileOffset(Sprite);
        }
    }
}
