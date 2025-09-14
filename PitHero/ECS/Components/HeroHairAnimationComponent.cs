using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Hair layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHairAnimationComponent : HeroAnimationComponent
    {
        protected override string DefaultAnimation => "HeroHairWalkDown";
        protected override string AnimDown => "HeroHairWalkDown";
        protected override string AnimLeft => "HeroHairWalkRight";   // Flipped in code
        protected override string AnimRight => "HeroHairWalkRight";
        protected override string AnimUp => "HeroHairWalkUp";
        protected override string JumpAnimDown => "HeroHairJumpDown";
        protected override string JumpAnimLeft => "HeroHairJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "HeroHairJumpRight";
        protected override string JumpAnimUp => "HeroHairJumpUp";
    }
}