using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Animation component that builds animation names from the enemy's EnemyId name.
    /// Uses the format [EnemyIdName]MoveDown/MoveRight/MoveUp from Actors.atlas.
    /// </summary>
    public class NamedMonsterAnimationComponent : EnemyAnimationComponent
    {
        private readonly string _animDown;
        private readonly string _animLeft;
        private readonly string _animRight;
        private readonly string _animUp;
        private readonly string _animAttack;

        public NamedMonsterAnimationComponent(string enemyIdName, Color color) : base(color)
        {
            _animDown = $"{enemyIdName}MoveDown";
            _animRight = $"{enemyIdName}MoveRight";
            _animLeft = $"{enemyIdName}MoveRight";
            _animUp = $"{enemyIdName}MoveUp";
            _animAttack = $"{enemyIdName}Attack";
        }

        protected override string DefaultAnimation => _animDown;
        protected override string AnimDown => _animDown;
        protected override string AnimLeft => _animLeft;
        protected override string AnimRight => _animRight;
        protected override string AnimUp => _animUp;
        protected override string AnimAttack => _animAttack;
    }
}
