using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Eyes layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroEyesAnimationComponent : HeroAnimationComponent
    {
        public HeroEyesAnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "MaleHeroEyesWalkDown";
        protected override string AnimDown => "MaleHeroEyesWalkDown";
        protected override string AnimLeft => "MaleHeroEyesWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroEyesWalkRight";
        protected override string AnimUp => "MaleHeroEyesWalkUp";
        protected override string JumpAnimDown => "MaleHeroEyesJumpRight";
        protected override string JumpAnimLeft => "MaleHeroEyesJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroEyesJumpRight";
        protected override string JumpAnimUp => "MaleHeroEyesJumpRight";

        protected override string SleepDown => "MaleHeroEyesSleepDown";
        protected override string SleepRight => "MaleHeroEyesSleepRight";  // Also used for SleepLeft (flipped in code)
    }
}
