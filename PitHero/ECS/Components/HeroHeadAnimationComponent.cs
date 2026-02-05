using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Head layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHeadAnimationComponent : HeroAnimationComponent
    {
        public HeroHeadAnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "MaleHeroHeadWalkDown";
        protected override string AnimDown => "MaleHeroHeadWalkDown";
        protected override string AnimLeft => "MaleHeroHeadWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroHeadWalkRight";
        protected override string AnimUp => "MaleHeroHeadWalkUp";
        protected override string JumpAnimDown => "MaleHeroHeadJumpRight";
        protected override string JumpAnimLeft => "MaleHeroHeadJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroHeadJumpRight";
        protected override string JumpAnimUp => "MaleHeroHeadJumpRight";
    }
}
