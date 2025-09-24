using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Slime-specific animation component that defines the animation names from the Actors.atlas
    /// </summary>
    public class SlimeAnimationComponent : EnemyAnimationComponent
    {
        public SlimeAnimationComponent(Color color = default) : base(color == default ? Color.White : color)
        {
        }

        protected override string DefaultAnimation => "SlimeWalkDown";
        protected override string AnimDown => "SlimeWalkDown";
        protected override string AnimLeft => "SlimeWalkLeft";
        protected override string AnimRight => "SlimeWalkRight";
        protected override string AnimUp => "SlimeWalkUp";
    }
}