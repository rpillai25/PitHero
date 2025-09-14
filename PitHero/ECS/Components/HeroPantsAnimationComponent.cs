using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Pants layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroPantsAnimationComponent : HeroAnimationComponent
    {
        protected override string DefaultAnimation => "HeroPantsWalkDown";
        protected override string AnimDown => "HeroPantsWalkDown";
        protected override string AnimLeft => "HeroPantsWalkRight";   // Flipped in code
        protected override string AnimRight => "HeroPantsWalkRight";
        protected override string AnimUp => "HeroPantsWalkUp";
        protected override string JumpAnimDown => "HeroPantsJumpDown";
        protected override string JumpAnimLeft => "HeroPantsJumpRight";  // Flipped in code
        protected override string JumpAnimRight => "HeroPantsJumpRight";
        protected override string JumpAnimUp => "HeroPantsJumpUp";
    }
}