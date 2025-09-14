using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

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

        protected override string DefaultAnimation => "HeroHand1WalkDown";
        protected override string AnimDown => "HeroHand1WalkDown";
        protected override string AnimLeft => "HeroHand1WalkRight";   // Flipped in code
        protected override string AnimRight => "HeroHand1WalkRight";
        protected override string AnimUp => "HeroHand1WalkUp";
        protected override string JumpAnimDown => "HeroHand1JumpDown";
        protected override string JumpAnimLeft => "HeroHand1JumpRight";  // Flipped in code
        protected override string JumpAnimRight => "HeroHand1JumpRight";
        protected override string JumpAnimUp => "HeroHand1JumpUp";
    }
}