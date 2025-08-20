using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Base class for all Hero GOAP actions
    /// </summary>
    public abstract class HeroActionBase : Action
    {
        protected HeroActionBase(string name, int cost = 1) : base(name, cost)
        {
        }

        /// <summary>
        /// Execute the action. Returns true when action is complete.
        /// </summary>
        public abstract bool Execute(HeroComponent hero);

        /// <summary>
        /// Helper method to move hero towards a target position
        /// </summary>
        protected bool MoveTowards(HeroComponent hero, Vector2 targetPosition, float deltaTime)
        {
            var currentPosition = hero.Entity.Transform.Position;
            var direction = Vector2.Normalize(targetPosition - currentPosition);
            var distance = Vector2.Distance(currentPosition, targetPosition);

            if (distance < 5f) // Close enough threshold
            {
                hero.Entity.Transform.Position = targetPosition;
                return true; // Reached target
            }

            // Move towards target
            var movement = direction * hero.MoveSpeed * deltaTime;
            hero.Entity.Transform.Position = currentPosition + movement;
            
            return false; // Still moving
        }

        /// <summary>
        /// Convert tile coordinates to world position
        /// </summary>
        protected Vector2 TileToWorldPosition(Point tileCoords, int tileSize = 64)
        {
            return new Vector2(tileCoords.X * tileSize + tileSize / 2, tileCoords.Y * tileSize + tileSize / 2);
        }

        /// <summary>
        /// Get world position of pit center
        /// </summary>
        protected Vector2 GetPitCenterWorldPosition()
        {
            return TileToWorldPosition(new Point(6, 6));
        }

        /// <summary>
        /// Get world position of map center
        /// </summary>
        protected Vector2 GetMapCenterWorldPosition()
        {
            // Assuming map center is at tile (10, 6) - adjust as needed
            return TileToWorldPosition(new Point(10, 6));
        }
    }
}