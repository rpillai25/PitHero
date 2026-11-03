using Microsoft.Xna.Framework;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Immutable struct holding the hero's appearance and job choices</summary>
    public readonly struct HeroDesign
    {
        /// <summary>Hero name</summary>
        public readonly string Name;

        /// <summary>Skin color (applies to the head and neck, and to the female body)</summary>
        public readonly Color SkinColor;

        /// <summary>Hair color</summary>
        public readonly Color HairColor;

        /// <summary>Hairstyle index (1-based, 1 to GameConfig.HeroHairstyleCount)</summary>
        public readonly int HairstyleIndex;

        /// <summary>Shirt color (the skirt on female heroes)</summary>
        public readonly Color ShirtColor;

        /// <summary>Eye color, tinting the iris layer</summary>
        public readonly Color EyeColor;

        /// <summary>Starting job name (e.g. "Knight", "Mage")</summary>
        public readonly string JobName;

        /// <summary>Hero gender; drives which first-name pool the name was drawn from</summary>
        public readonly Gender Gender;

        /// <summary>Creates a new HeroDesign with the specified appearance and job choices</summary>
        public HeroDesign(string name, Color skinColor, Color hairColor, int hairstyleIndex, Color shirtColor, string jobName = "Knight", Gender gender = Gender.Male, Color? eyeColor = null)
        {
            Name = name;
            Gender = gender;
            SkinColor = skinColor;
            HairColor = hairColor;
            HairstyleIndex = hairstyleIndex;
            ShirtColor = shirtColor;
            EyeColor = eyeColor ?? PitHero.GameConfig.DefaultEyeColor;
            JobName = string.IsNullOrEmpty(jobName) ? "Knight" : jobName;
        }
    }
}
