using Microsoft.Xna.Framework;
using RolePlayingFramework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages the hero's colored iris layer, drawn over HeroEyesBaseAnimationComponent, using the
    /// Actors.atlas. Its sleep frames are empty, so the iris disappears while asleep.
    /// </summary>
    public class HeroEyesAnimationComponent : HeroAnimationComponent
    {
        public HeroEyesAnimationComponent(Color color, Gender gender = Gender.Male)
            : base(color, HeroSpriteNames.Get(gender, HeroLayer.Eyes))
        {
        }
    }
}
