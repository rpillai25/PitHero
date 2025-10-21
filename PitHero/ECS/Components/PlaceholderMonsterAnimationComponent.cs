using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Generic placeholder monster animation component using PlaceholderMonster sprite.
    /// This allows for easy future sprite replacement by subclassing or updating the animation names.
    /// </summary>
    public class PlaceholderMonsterAnimationComponent : EnemyAnimationComponent
    {
        public PlaceholderMonsterAnimationComponent(Color color = default) : base(color == default ? Color.White : color)
        {
        }

        // PlaceholderMonster is a static sprite, so we use the same sprite for all directions
        protected override string DefaultAnimation => "PlaceholderMonster";
        protected override string AnimDown => "PlaceholderMonster";
        protected override string AnimLeft => "PlaceholderMonster";
        protected override string AnimRight => "PlaceholderMonster";
        protected override string AnimUp => "PlaceholderMonster";
    }
}
