using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Hand1 layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHand1AnimationComponent : HeroAnimationComponent
    {
        public HeroHand1AnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "MaleHeroArmWalkDown";
        protected override string AnimDown => "MaleHeroArmWalkDown";
        protected override string AnimLeft => "MaleHeroArmWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroArmWalkRight";
        protected override string AnimUp => "MaleHeroArmWalkUp";
        protected override string JumpAnimDown => "MaleHeroArmJumpRight";
        protected override string JumpAnimLeft => "MaleHeroArmJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroArmJumpRight";
        protected override string JumpAnimUp => "MaleHeroArmJumpRight";
    }
}