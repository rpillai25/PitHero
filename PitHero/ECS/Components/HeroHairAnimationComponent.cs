using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Hair layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHairAnimationComponent : HeroAnimationComponent
    {
        public HeroHairAnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "MaleHeroHairWalkDown";
        protected override string AnimDown => "MaleHeroHairWalkDown";
        protected override string AnimLeft => "MaleHeroHairWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroHairWalkRight";
        protected override string AnimUp => "MaleHeroHairWalkUp";
        protected override string JumpAnimDown => "MaleHeroHairJumpRight";
        protected override string JumpAnimLeft => "MaleHeroHairJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroHairJumpRight";
        protected override string JumpAnimUp => "MaleHeroHairJumpRight";
    }
}