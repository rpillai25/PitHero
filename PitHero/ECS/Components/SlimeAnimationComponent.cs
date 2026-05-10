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

        protected override string DefaultAnimation => "SlimeMoveDown";
        protected override string AnimDown => "SlimeMoveDown";
        protected override string AnimLeft => "SlimeMoveRight";
        protected override string AnimRight => "SlimeMoveRight";
        protected override string AnimUp => "SlimeMoveUp";
    }
}