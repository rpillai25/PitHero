using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework;

namespace PitHero.UI
{
    /// <summary>Composite IDrawable that renders all hero sprite layers with tint colors for UI display.</summary>
    public class HeroPreviewDrawable : Nez.UI.IDrawable
    {
        /// <summary>Number of hero sprite layers drawn back-to-front.</summary>
        private const int LayerCount = 7;

        private readonly Sprite[] _sprites;
        private readonly Color[] _tints;

        public float LeftWidth { get; set; }
        public float RightWidth { get; set; }
        public float TopHeight { get; set; }
        public float BottomHeight { get; set; }
        public float MinWidth { get; set; }
        public float MinHeight { get; set; }

        /// <summary>Creates a composite drawable from the Actors atlas using saved hero design data.</summary>
        public HeroPreviewDrawable(SpriteAtlas actorsAtlas, Color skinColor, Color hairColor, Color shirtColor, int hairstyleIndex, Gender gender = Gender.Male)
        {
            _sprites = new Sprite[LayerCount];
            _tints = new Color[LayerCount];

            // Draw order: back-to-front (index 0 drawn first = backmost)
            SetLayer(actorsAtlas, 0, gender, HeroLayer.Body, HeroSpriteNames.BodyTint(gender, skinColor));
            if (HeroSpriteNames.HasArmorLayer(gender))
                SetLayer(actorsAtlas, 1, gender, HeroLayer.Armor, Color.White);
            SetLayer(actorsAtlas, 2, gender, HeroLayer.Clothing, shirtColor);
            SetLayer(actorsAtlas, 3, gender, HeroLayer.Head, skinColor);
            SetLayer(actorsAtlas, 4, gender, HeroLayer.EyesBase, Color.White);
            SetLayer(actorsAtlas, 5, gender, HeroLayer.Eyes, Color.White);
            SetLayer(actorsAtlas, 6, gender, HeroSpriteNames.HairLayer(hairstyleIndex), hairColor);

            // Set minimum size from the first valid sprite (all hero frames are 32x48)
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

        private void SetLayer(SpriteAtlas actorsAtlas, int index, Gender gender, HeroLayer layer, Color tint)
        {
            _sprites[index] = actorsAtlas.GetSprite(HeroSpriteNames.Get(gender, layer).PreviewSprite);
            _tints[index] = tint;
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
