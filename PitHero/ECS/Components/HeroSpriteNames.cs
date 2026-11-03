using Microsoft.Xna.Framework;
using RolePlayingFramework;
using System;

namespace PitHero.ECS.Components
{
    /// <summary>Paperdoll layers that have frames in the Actors atlas.</summary>
    public enum HeroLayer
    {
        Body = 0,
        Clothing = 1,
        Head = 2,
        EyesBase = 3,
        Eyes = 4,
        Hair1 = 5,
        Hair2 = 6,
        Hair3 = 7,
        Hair4 = 8,
        Armor = 9
    }

    /// <summary>Atlas animation names of one paperdoll layer.</summary>
    public sealed class HeroLayerNames
    {
        /// <summary>Move-down animation name.</summary>
        public string Down;
        /// <summary>Move-right animation name, flipped in code for left.</summary>
        public string Right;
        /// <summary>Move-up animation name.</summary>
        public string Up;
        /// <summary>Jump-right animation name, used for every jump direction.</summary>
        public string Jump;
        /// <summary>Sleep-down animation name; layers without sleep art hold their move-down pose.</summary>
        public string SleepDown;
        /// <summary>Sleep-right animation name; layers without sleep art hold their move-right pose.</summary>
        public string SleepRight;
        /// <summary>Name of the first move-down frame, used by static previews.</summary>
        public string PreviewSprite;
    }

    /// <summary>
    /// Pre-built atlas names for every gender and paperdoll layer, so no name is concatenated during
    /// gameplay. Folders in Content/Textures/Actors follow the same scheme: MaleHeroBodyMoveDown holds
    /// male_hero_body_move_down_0.png and on up.
    /// </summary>
    public static class HeroSpriteNames
    {
        private const int GenderCount = 2;
        private const int LayerCount = 10;

        private static readonly HeroLayerNames[] Table = BuildTable();

        /// <summary>Gets the atlas names of a layer for a gender.</summary>
        public static HeroLayerNames Get(Gender gender, HeroLayer layer)
        {
            return Table[(int)gender * LayerCount + (int)layer];
        }

        /// <summary>Gets the hair layer for a 1-based hairstyle index, clamped to the hairstyles that exist.</summary>
        public static HeroLayer HairLayer(int hairstyleIndex)
        {
            return HeroLayer.Hair1 + (GameConfig.ClampHairstyleIndex(hairstyleIndex) - 1);
        }

        /// <summary>True when a gender has a separate Armor layer; male armor is painted into Body.</summary>
        public static bool HasArmorLayer(Gender gender)
        {
            return gender == Gender.Female;
        }

        /// <summary>Gets the Body layer tint: female bodies are gray skin and take the skin color, male bodies have painted colors.</summary>
        public static Color BodyTint(Gender gender, Color skinColor)
        {
            return gender == Gender.Female ? skinColor : Color.White;
        }

        private static HeroLayerNames[] BuildTable()
        {
            var table = new HeroLayerNames[GenderCount * LayerCount];
            for (int g = 0; g < GenderCount; g++)
            {
                var gender = (Gender)g;
                for (int l = 0; l < LayerCount; l++)
                {
                    var layer = (HeroLayer)l;
                    table[g * LayerCount + l] = BuildNames(gender, layer);
                }
            }
            return table;
        }

        private static HeroLayerNames BuildNames(Gender gender, HeroLayer layer)
        {
            string prefix = gender == Gender.Female ? "FemaleHero" : "MaleHero";
            string filePrefix = gender == Gender.Female ? "female_hero" : "male_hero";
            string layerName = LayerName(gender, layer);
            string name = prefix + layerName;
            bool hasSleepArt = layer == HeroLayer.Eyes || layer == HeroLayer.EyesBase;

            return new HeroLayerNames
            {
                Down = name + "MoveDown",
                Right = name + "MoveRight",
                Up = name + "MoveUp",
                Jump = name + "JumpRight",
                SleepDown = hasSleepArt ? name + "SleepDown" : name + "MoveDown",
                SleepRight = hasSleepArt ? name + "SleepRight" : name + "MoveRight",
                PreviewSprite = filePrefix + "_" + FileLayerName(layerName) + "_move_down_0"
            };
        }

        private static string LayerName(Gender gender, HeroLayer layer)
        {
            switch (layer)
            {
                case HeroLayer.Body: return "Body";
                case HeroLayer.Clothing: return gender == Gender.Female ? "Skirt" : "Shirt";
                case HeroLayer.Head: return "Head";
                case HeroLayer.EyesBase: return "EyesBase";
                case HeroLayer.Eyes: return "Eyes";
                case HeroLayer.Hair1: return "Hair1";
                case HeroLayer.Hair2: return "Hair2";
                case HeroLayer.Hair3: return "Hair3";
                case HeroLayer.Hair4: return "Hair4";
                case HeroLayer.Armor: return "Armor";
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private static string FileLayerName(string layerName)
        {
            return layerName == "EyesBase" ? "eyes_base" : layerName.ToLowerInvariant();
        }
    }
}
