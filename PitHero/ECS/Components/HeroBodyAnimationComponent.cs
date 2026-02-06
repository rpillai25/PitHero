using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Body layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroBodyAnimationComponent : HeroAnimationComponent
    {
        public HeroBodyAnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "MaleHeroBodyWalkDown";
        protected override string AnimDown => "MaleHeroBodyWalkDown";
        protected override string AnimLeft => "MaleHeroBodyWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroBodyWalkRight";
        protected override string AnimUp => "MaleHeroBodyWalkUp";
        protected override string JumpAnimDown => "MaleHeroBodyJumpRight";
        protected override string JumpAnimLeft => "MaleHeroBodyJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroBodyJumpRight";
        protected override string JumpAnimUp => "MaleHeroBodyJumpRight";
    }
}