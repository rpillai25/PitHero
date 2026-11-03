using Microsoft.Xna.Framework;
using RolePlayingFramework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages the hero clothing layer sprite animations (male Shirt, female Skirt) based on movement
    /// direction using the Actors.atlas
    /// </summary>
    public class HeroShirtAnimationComponent : HeroAnimationComponent
    {
        public HeroShirtAnimationComponent(Color color, Gender gender = Gender.Male)
            : base(color, HeroSpriteNames.Get(gender, HeroLayer.Clothing))
        {
        }
    }
}
