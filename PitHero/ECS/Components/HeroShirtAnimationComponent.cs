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

        protected override string DefaultAnimation => "MaleHeroShirtWalkDown";
        protected override string AnimDown => "MaleHeroShirtWalkDown";
        protected override string AnimLeft => "MaleHeroShirtWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroShirtWalkRight";
        protected override string AnimUp => "MaleHeroShirtWalkUp";
        protected override string JumpAnimDown => "MaleHeroShirtJumpRight";
        protected override string JumpAnimLeft => "MaleHeroShirtJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroShirtJumpRight";
        protected override string JumpAnimUp => "MaleHeroShirtJumpRight";

    }
}