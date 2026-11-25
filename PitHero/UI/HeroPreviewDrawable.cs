using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>Composite IDrawable that renders all hero sprite layers with tint colors for UI display.</summary>
    public class HeroPreviewDrawable : Nez.UI.IDrawable
    {
        /// <summary>Most sprites drawn back-to-front: every tone layer of the seven paperdoll parts (issue #462).</summary>
        private const int MaxSprites = 16;

        private readonly Sprite[] _sprites = new Sprite[MaxSprites];
        private readonly Color[] _tints = new Color[MaxSprites];
        private int _count;

        public float LeftWidth { get; set; }
        public float RightWidth { get; set; }
        public float TopHeight { get; set; }
        public float BottomHeight { get; set; }
        public float MinWidth { get; set; }
        public float MinHeight { get; set; }

        /// <summary>Creates a composite drawable from the Actors atlas using saved hero design data.</summary>
        public HeroPreviewDrawable(SpriteAtlas actorsAtlas, Color skinColor, Color hairColor, Color shirtColor, int hairstyleIndex, Gender gender, Color eyeColor,
            HeroCustomTones skinTones = default, HeroCustomTones hairTones = default, HeroCustomTones shirtTones = default)
        {
            // Draw order: back-to-front (index 0 drawn first = backmost)
            AddLayer(actorsAtlas, gender, HeroLayer.Body, HeroSpriteNames.BodyTint(gender, skinColor), skinTones);
            if (HeroSpriteNames.HasArmorLayer(gender))
                AddLayer(actorsAtlas, gender, HeroLayer.Armor, Color.White, default);
            AddLayer(actorsAtlas, gender, HeroLayer.Clothing, shirtColor, shirtTones);
            AddLayer(actorsAtlas, gender, HeroLayer.Head, skinColor, skinTones);
            AddLayer(actorsAtlas, gender, HeroLayer.EyesBase, Color.White, default);
            AddLayer(actorsAtlas, gender, HeroLayer.Eyes, eyeColor, default);
            AddLayer(actorsAtlas, gender, HeroSpriteNames.HairLayer(hairstyleIndex), hairColor, hairTones);

            // Set minimum size from the first valid sprite (all hero frames are 32x48)
            for (int i = 0; i < _count; i++)
            {
                if (_sprites[i] != null)
                {
                    MinWidth = _sprites[i].SourceRect.Width;
                    MinHeight = _sprites[i].SourceRect.Height;
                    break;
                }
            }
        }

        // Adds every tone layer of a part, each tinted from the part's chosen color (issue #462)
        private void AddLayer(SpriteAtlas actorsAtlas, Gender gender, HeroLayer layer, Color tint, in HeroCustomTones custom)
        {
            var tones = HeroSpriteNames.Get(gender, layer).Tones;
            for (int i = 0; i < tones.Length && _count < MaxSprites; i++)
            {
                _sprites[_count] = actorsAtlas.GetSprite(tones[i].PreviewSprite);
                _tints[_count] = HeroToneProfiles.Tint(gender, layer, tones[i].Tone, tint, in custom);
                _count++;
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
            for (int i = 0; i < _count; i++)
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
