using Microsoft.Xna.Framework;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Immutable struct holding the hero's appearance and job choices</summary>
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

        /// <summary>Starting job name (e.g. "Knight", "Mage")</summary>
        public readonly string JobName;

        /// <summary>Creates a new HeroDesign with the specified appearance and job choices</summary>
        public HeroDesign(string name, Color skinColor, Color hairColor, int hairstyleIndex, Color shirtColor, string jobName = "Knight")
        {
            Name = name;
            SkinColor = skinColor;
            HairColor = hairColor;
            HairstyleIndex = hairstyleIndex;
            ShirtColor = shirtColor;
            JobName = string.IsNullOrEmpty(jobName) ? "Knight" : jobName;
        }
    }
}
