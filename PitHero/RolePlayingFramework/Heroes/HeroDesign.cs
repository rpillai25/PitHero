using Microsoft.Xna.Framework;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Immutable struct holding the hero's appearance choices</summary>
    public readonly struct HeroDesign
    {
        /// <summary>Hero name</summary>
        public readonly string Name;

        /// <summary>Skin color (applies to body, head, hands)</summary>
        public readonly Color SkinColor;

        /// <summary>Hair color</summary>
        public readonly Color HairColor;

        /// <summary>Hairstyle index (1-based, 1-5)</summary>
        public readonly int HairstyleIndex;

        /// <summary>Shirt color</summary>
        public readonly Color ShirtColor;

        /// <summary>Creates a new HeroDesign with the specified appearance choices</summary>
        public HeroDesign(string name, Color skinColor, Color hairColor, int hairstyleIndex, Color shirtColor)
        {
            Name = name;
            SkinColor = skinColor;
            HairColor = hairColor;
            HairstyleIndex = hairstyleIndex;
            ShirtColor = shirtColor;
        }
    }
}
