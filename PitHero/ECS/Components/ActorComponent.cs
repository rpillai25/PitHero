using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Base component for all actors that can trigger collision events
    /// Implements ITriggerListener to handle collision detection
    /// </summary>
    public abstract class ActorComponent : Component, ITriggerListener
    {
        public float Health { get; set; } = 100f;
        public float MaxHealth { get; set; } = 100f;
        public Vector2 Velocity { get; set; }
        public bool IsAlive => Health > 0f;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            // Initialize any actor-specific logic
        }

        /// <summary>
        /// Called when this actor enters a trigger collider
        /// </summary>
        public virtual void OnTriggerEnter(Collider other, Collider local)
        {
            // Override in derived classes to handle specific collision logic
        }

        /// <summary>
        /// Called when this actor exits a trigger collider
        /// </summary>
        public virtual void OnTriggerExit(Collider other, Collider local)
        {
            // Override in derived classes to handle specific collision logic
        }

        /// <summary>
        /// Check if the collider is from a TileMap collision layer
        /// </summary>
        protected bool IsTileMapCollision(Collider collider)
        {
            return collider.PhysicsLayer == GameConfig.PhysicsTileMapLayer;
        }

        /// <summary>
        /// Get the tile coordinates from a world position
        /// </summary>
        protected Point GetTileCoordinates(Vector2 worldPosition, int tileSize = 64)
        {
            return new Point((int)(worldPosition.X / tileSize), (int)(worldPosition.Y / tileSize));
        }
    }
}