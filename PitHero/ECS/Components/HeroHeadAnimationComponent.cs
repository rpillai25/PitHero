using Microsoft.Xna.Framework;
using RolePlayingFramework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Head layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHeadAnimationComponent : HeroAnimationComponent
    {
        public HeroHeadAnimationComponent(Color color, Gender gender = Gender.Male)
            : base(color, HeroSpriteNames.Get(gender, HeroLayer.Head))
        {
        }
    }
}
