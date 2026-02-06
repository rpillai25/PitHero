using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Pants layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroPantsAnimationComponent : HeroAnimationComponent
    {
        public HeroPantsAnimationComponent(Color color) : base(color)
        {
        }

        protected override string DefaultAnimation => "MaleHeroPantsWalkDown";
        protected override string AnimDown => "MaleHeroPantsWalkDown";
        protected override string AnimLeft => "MaleHeroPantsWalkRight";   // Flipped in code
        protected override string AnimRight => "MaleHeroPantsWalkRight";
        protected override string AnimUp => "MaleHeroPantsWalkUp";
        protected override string JumpAnimDown => "MaleHeroPantsJumpRight";
        protected override string JumpAnimLeft => "MaleHeroPantsJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "MaleHeroPantsJumpRight";
        protected override string JumpAnimUp => "MaleHeroPantsJumpRight";
    }
}