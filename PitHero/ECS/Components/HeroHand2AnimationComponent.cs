using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Hand2 layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHand2AnimationComponent : HeroAnimationComponent
    {
        protected override string DefaultAnimation => "HeroHand2WalkDown";
        protected override string AnimDown => "HeroHand2WalkDown";
        protected override string AnimLeft => "HeroHand2WalkRight";   // Flipped in code
        protected override string AnimRight => "HeroHand2WalkRight";
        protected override string AnimUp => "HeroHand2WalkUp";
        protected override string JumpAnimDown => "HeroHand2JumpDown";
        protected override string JumpAnimLeft => "HeroHand2JumpRight";  // Flipped in code
        protected override string JumpAnimRight => "HeroHand2JumpRight";
        protected override string JumpAnimUp => "HeroHand2JumpUp";
    }
}