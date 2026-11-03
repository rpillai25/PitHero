using Microsoft.Xna.Framework;
using RolePlayingFramework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages the hero's eye outline and whites using the Actors.atlas, or the closed-eye slits while
    /// asleep. Keeps its painted colors, so it is always tinted White.
    /// </summary>
    public class HeroEyesBaseAnimationComponent : HeroAnimationComponent
    {
        public HeroEyesBaseAnimationComponent(Gender gender = Gender.Male)
            : base(Color.White, HeroSpriteNames.Get(gender, HeroLayer.EyesBase))
        {
        }
    }
}
