using Microsoft.Xna.Framework;
using Nez;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A mover that constrains movement to tile-by-tile increments in cardinal directions only.
    /// Movement is in 32-pixel increments and entities are snapped to tile coordinates.
    /// Handles trigger detection and prevents movement through solid colliders.
    /// </summary>
    public class TileByTileMover : Component
    {
        private ColliderTriggerHelper _triggerHelper;
        private readonly int _tileSize = GameConfig.TileSize;
        
        /// <summary>
        /// If true, movement is currently in progress and new movement requests will be ignored
        /// </summary>
        public bool IsMoving { get; private set; }

        public override void OnAddedToEntity()
        {
            _triggerHelper = new ColliderTriggerHelper(Entity);
        }

        /// <summary>
        /// Attempt to move the entity one tile in the specified direction
        /// </summary>
        /// <param name="direction">Cardinal direction to move (Up, Down, Left, Right)</param>
        /// <returns>True if movement was successful, false if blocked</returns>
        public bool TryMoveInDirection(Direction direction)
        {
            if (IsMoving)
                return false;

            var motion = GetMotionVector(direction);
            return TryMove(motion);
        }

        /// <summary>
        /// Attempt to move the entity by the specified motion vector (will be snapped to tile boundaries)
        /// </summary>
        /// <param name="motion">Movement vector</param>
        /// <returns>True if movement was successful, false if blocked</returns>
        public bool TryMove(Vector2 motion)
        {
            if (IsMoving)
                return false;

            // Snap motion to tile increments in cardinal directions only
            motion = SnapMotionToTile(motion);
            
            if (motion == Vector2.Zero)
                return false;

            // Get the entity's collider
            var collider = Entity.GetComponent<Collider>();
            if (collider == null)
            {
                // No collider, just move directly
                Entity.Transform.Position += motion;
                SnapToTileGrid();
                _triggerHelper?.Update();
                return true;
            }

            // Check for collisions using Nez's collision system
            CollisionResult collisionResult;
            if (CalculateMovement(ref motion, out collisionResult))
            {
                // Hit a solid collider, movement blocked
                return false;
            }

            // Apply the movement
            ApplyMovement(motion);
            return true;
        }

        /// <summary>
        /// Calculates movement taking collisions into account, based on Nez.Mover.CalculateMovement
        /// </summary>
        private bool CalculateMovement(ref Vector2 motion, out CollisionResult collisionResult)
        {
            collisionResult = new CollisionResult();

            var collider = Entity.GetComponent<Collider>();
            if (collider == null || _triggerHelper == null)
                return false;

            // Fetch anything that we might collide with at our new position
            var bounds = collider.Bounds;
            bounds.X += motion.X;
            bounds.Y += motion.Y;
            var neighbors = Physics.BoxcastBroadphaseExcludingSelf(collider, ref bounds, collider.CollidesWithLayers);

            foreach (var neighbor in neighbors)
            {
                // Skip triggers - we want to move through them but still detect them
                if (neighbor.IsTrigger)
                    continue;

                if (collider.CollidesWith(neighbor, motion, out CollisionResult internalCollisionResult))
                {
                    // Hit a solid collider - movement is blocked
                    collisionResult = internalCollisionResult;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies movement and updates trigger detection, based on Nez.Mover.ApplyMovement
        /// </summary>
        private void ApplyMovement(Vector2 motion)
        {
            IsMoving = true;
            
            // Move entity to its new position
            Entity.Transform.Position += motion;
            
            // Snap to tile grid to ensure precise positioning
            SnapToTileGrid();

            // Update trigger detection - this will fire ITriggerListener events
            _triggerHelper?.Update();
            
            IsMoving = false;
        }

        /// <summary>
        /// Snap motion to tile increments and cardinal directions only
        /// </summary>
        private Vector2 SnapMotionToTile(Vector2 motion)
        {
            // Determine the dominant direction
            var absX = System.Math.Abs(motion.X);
            var absY = System.Math.Abs(motion.Y);

            if (absX > absY)
            {
                // Horizontal movement
                return new Vector2(motion.X > 0 ? _tileSize : -_tileSize, 0);
            }
            else if (absY > absX)
            {
                // Vertical movement
                return new Vector2(0, motion.Y > 0 ? _tileSize : -_tileSize);
            }

            // No clear dominant direction or zero motion
            return Vector2.Zero;
        }

        /// <summary>
        /// Snap entity position to tile grid boundaries
        /// </summary>
        private void SnapToTileGrid()
        {
            var pos = Entity.Transform.Position;
            var snappedX = System.Math.Round(pos.X / _tileSize) * _tileSize;
            var snappedY = System.Math.Round(pos.Y / _tileSize) * _tileSize;
            Entity.Transform.Position = new Vector2((float)snappedX, (float)snappedY);
        }

        /// <summary>
        /// Convert direction enum to motion vector
        /// </summary>
        private Vector2 GetMotionVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return new Vector2(0, -_tileSize);
                case Direction.Down:
                    return new Vector2(0, _tileSize);
                case Direction.Left:
                    return new Vector2(-_tileSize, 0);
                case Direction.Right:
                    return new Vector2(_tileSize, 0);
                default:
                    return Vector2.Zero;
            }
        }

        /// <summary>
        /// Get current tile coordinates of the entity
        /// </summary>
        public Point GetCurrentTileCoordinates()
        {
            var pos = Entity.Transform.Position;
            return new Point((int)(pos.X / _tileSize), (int)(pos.Y / _tileSize));
        }
    }

    /// <summary>
    /// Cardinal directions for tile-based movement
    /// </summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }
}