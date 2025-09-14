using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Body layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroBodyAnimationComponent : HeroAnimationComponent
    {
        protected override string DefaultAnimation => "HeroBodyWalkDown";
        protected override string AnimDown => "HeroBodyWalkDown";
        protected override string AnimLeft => "HeroBodyWalkRight";   // Flipped in code
        protected override string AnimRight => "HeroBodyWalkRight";
        protected override string AnimUp => "HeroBodyWalkUp";
        protected override string JumpAnimDown => "HeroBodyJumpDown";
        protected override string JumpAnimLeft => "HeroBodyJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "HeroBodyJumpRight";
        protected override string JumpAnimUp => "HeroBodyJumpUp";
    }
}