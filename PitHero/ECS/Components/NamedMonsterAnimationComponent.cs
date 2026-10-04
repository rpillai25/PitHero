using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Animation component that builds animation names from the enemy's EnemyId name.
    /// Uses the format [EnemyIdName]MoveRight from Actors.atlas (flipped for leftward movement).
    /// </summary>
    public class NamedMonsterAnimationComponent : EnemyAnimationComponent
    {
        private readonly string _animMoveRight;
        private readonly string _animAttack;

        public NamedMonsterAnimationComponent(string enemyIdName, Color color) : base(color)
        {
            _animMoveRight = $"{enemyIdName}MoveRight";
            _animAttack = $"{enemyIdName}Attack";
        }

        protected override string AnimMoveRight => _animMoveRight;
        protected override string AnimAttack => _animAttack;
    }
}
