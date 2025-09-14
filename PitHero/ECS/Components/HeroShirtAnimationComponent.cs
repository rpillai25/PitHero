using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Shirt layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroShirtAnimationComponent : HeroAnimationComponent
    {
        public HeroShirtAnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "HeroShirtWalkDown";
        protected override string AnimDown => "HeroShirtWalkDown";
        protected override string AnimLeft => "HeroShirtWalkRight";   // Flipped in code
        protected override string AnimRight => "HeroShirtWalkRight";
        protected override string AnimUp => "HeroShirtWalkUp";
        protected override string JumpAnimDown => "HeroShirtJumpDown";
        protected override string JumpAnimLeft => "HeroShirtJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "HeroShirtJumpRight";
        protected override string JumpAnimUp => "HeroShirtJumpUp";

    }
}