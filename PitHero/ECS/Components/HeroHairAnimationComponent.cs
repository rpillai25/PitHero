using Microsoft.Xna.Framework;
using RolePlayingFramework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Hair layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHairAnimationComponent : HeroAnimationComponent
    {
        /// <summary>
        /// Creates a new HeroHairAnimationComponent with the specified color and hairstyle
        /// </summary>
        /// <param name="color">The color tint for the hair</param>
        /// <param name="hairstyleIndex">The hairstyle index (1-based), where 1 is the default hairstyle</param>
        /// <param name="gender">The hero's gender, which selects the hair art</param>
        public HeroHairAnimationComponent(Color color, int hairstyleIndex = 1, Gender gender = Gender.Male)
            : base(color, HeroSpriteNames.Get(gender, HeroSpriteNames.HairLayer(hairstyleIndex)))
        {
        }
    }
}
