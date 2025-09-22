using Nez;
using RolePlayingFramework.Enemies;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that represents an enemy in the game world.
    /// Links an entity to an IEnemy implementation from the RolePlayingFramework.
    /// </summary>
    public class EnemyComponent : Component
    {
        /// <summary>
        /// The underlying enemy data and behavior
        /// </summary>
        public IEnemy Enemy { get; set; }

        /// <summary>
        /// Whether this enemy is stationary (doesn't move when hero moves)
        /// </summary>
        public bool IsStationary { get; set; }

        /// <summary>
        /// Whether this enemy is currently moving (to prevent multiple movements)
        /// </summary>
        public bool IsMoving { get; set; }

        /// <summary>
        /// Number of hero moves required before this enemy moves (random 4-8)
        /// </summary>
        public int MoveCooldown { get; set; }

        /// <summary>
        /// Current count of hero moves since last enemy move
        /// </summary>
        public int MoveCounter { get; set; }

        public EnemyComponent(IEnemy enemy, bool isStationary = false)
        {
            Enemy = enemy;
            IsStationary = isStationary;
            IsMoving = false;
            MoveCooldown = Nez.Random.Range(4, 9); // Random 4-8
            MoveCounter = 0;
        }

        /// <summary>
        /// Reset the move cooldown to a new random value
        /// </summary>
        public void ResetMoveCooldown()
        {
            MoveCooldown = Nez.Random.Range(4, 9); // Random 4-8
            MoveCounter = 0;
        }
    }
}