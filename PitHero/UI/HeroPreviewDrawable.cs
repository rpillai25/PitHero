using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>Composite IDrawable that renders all hero sprite layers with tint colors for UI display.</summary>
    public class HeroPreviewDrawable : Nez.UI.IDrawable
    {
        /// <summary>Number of hero sprite layers drawn back-to-front.</summary>
        private const int LayerCount = 8;

        private readonly Sprite[] _sprites;
        private readonly Color[] _tints;

        public float LeftWidth { get; set; }
        public float RightWidth { get; set; }
        public float TopHeight { get; set; }
        public float BottomHeight { get; set; }
        public float MinWidth { get; set; }
        public float MinHeight { get; set; }

        /// <summary>Creates a composite drawable from the Actors atlas using saved hero design data.</summary>
        public HeroPreviewDrawable(SpriteAtlas actorsAtlas, Color skinColor, Color hairColor, Color shirtColor, int hairstyleIndex)
        {
            _sprites = new Sprite[LayerCount];
            _tints = new Color[LayerCount];

            // Hair sprite name varies by hairstyle index (1 = no suffix, 2+ = suffix)
            string hairSuffix = hairstyleIndex == 1 ? "" : hairstyleIndex.ToString();

            // Draw order: back-to-front (index 0 drawn first = backmost)
            // 0: Back arm (Hand2)
            _sprites[0] = actorsAtlas.GetSprite("male_hero_back_arm_walk_down_0");
            _tints[0] = skinColor;

            // 1: Body
            _sprites[1] = actorsAtlas.GetSprite("male_hero_body_walk_down_0");
            _tints[1] = skinColor;

            // 2: Pants
            _sprites[2] = actorsAtlas.GetSprite("male_hero_pants_walk_down_0");
            _tints[2] = Color.White;

            // 3: Shirt
            _sprites[3] = actorsAtlas.GetSprite("male_hero_shirt_walk_down_0");
            _tints[3] = shirtColor;

            // 4: Head
            _sprites[4] = actorsAtlas.GetSprite("male_hero_head_walk_down_0");
            _tints[4] = skinColor;

            // 5: Eyes
            _sprites[5] = actorsAtlas.GetSprite("male_hero_eyes_walk_down_0");
            _tints[5] = Color.White;

            // 6: Hair
            _sprites[6] = actorsAtlas.GetSprite("male_hero_hair" + hairSuffix + "_walk_down_0");
            _tints[6] = hairColor;

            // 7: Front arm (Hand1)
            _sprites[7] = actorsAtlas.GetSprite("male_hero_arm_walk_down_0");
            _tints[7] = skinColor;

            // Set minimum size from the first valid sprite (all hero frames are 32x46)
            for (int i = 0; i < LayerCount; i++)
            {
                if (_sprites[i] != null)
                {
                    MinWidth = _sprites[i].SourceRect.Width;
                    MinHeight = _sprites[i].SourceRect.Height;
                    break;
                }
            }
        }

        /// <summary>Sets padding values for the drawable.</summary>
        public void SetPadding(float top, float bottom, float left, float right)
        {
            TopHeight = top;
            BottomHeight = bottom;
            LeftWidth = left;
            RightWidth = right;
        }

        /// <summary>Draws all hero sprite layers composited at the given position.</summary>
        public void Draw(Batcher batcher, float x, float y, float width, float height, Color color)
        {
            var destRect = new Rectangle((int)x, (int)y, (int)width, (int)height);
            for (int i = 0; i < LayerCount; i++)
            {
                if (_sprites[i] == null)
                    continue;

                // Multiply the base color with the layer tint
                var layerColor = color.Multiply(_tints[i]);
                batcher.Draw(_sprites[i], destRect, _sprites[i].SourceRect, layerColor);
            }
        }
    }
}
