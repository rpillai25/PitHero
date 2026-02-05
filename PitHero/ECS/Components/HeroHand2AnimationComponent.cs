using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Hand2 layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHand2AnimationComponent : HeroAnimationComponent
    {
        public HeroHand2AnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "MaleHeroBackArmWalkDown";
        protected override string AnimDown => "MaleHeroBackArmWalkDown";
        protected override string AnimLeft => "MaleHeroBackArmWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroBackArmWalkRight";
        protected override string AnimUp => "MaleHeroBackArmWalkUp";
        protected override string JumpAnimDown => "MaleHeroBackArmJumpRight";
        protected override string JumpAnimLeft => "MaleHeroBackArmJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroBackArmJumpRight";
        protected override string JumpAnimUp => "MaleHeroBackArmJumpRight";
    }
}