using Microsoft.Xna.Framework;
using RolePlayingFramework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Body layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroBodyAnimationComponent : HeroAnimationComponent
    {
        public HeroBodyAnimationComponent(Color color, Gender gender = Gender.Male)
            : base(color, HeroSpriteNames.Get(gender, HeroLayer.Body))
        {
        }
    }
}
