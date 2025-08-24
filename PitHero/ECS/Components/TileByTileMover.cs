using Microsoft.Xna.Framework;
using Nez;
using System.Collections.Generic;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A mover that moves entities in tile-based increments over time, respecting movement speed.
    /// Movement is constrained to cardinal directions and entities are snapped to tile boundaries.
    /// Handles trigger detection and prevents movement through solid colliders.
    /// </summary>
    public class TileByTileMover : Component, IUpdatable, IPausableComponent
    {
        private ColliderTriggerHelper _triggerHelper;
        private readonly int _tileSize = GameConfig.TileSize;
        
        /// <summary>
        /// Movement speed in tiles per second
        /// </summary>
        public float MovementSpeed { get; set; } = 2.0f; // 2 tiles per second by default
        
        /// <summary>
        /// If true, movement is currently in progress
        /// </summary>
        public bool IsMoving { get; private set; }
        
        /// <summary>
        /// Current movement direction (null if not moving)
        /// </summary>
        public Direction? CurrentDirection { get; private set; }
        
        /// <summary>
        /// Starting position of current movement
        /// </summary>
        private Vector2 _moveStartPosition;
        
        /// <summary>
        /// Target position of current movement
        /// </summary>
        private Vector2 _moveTargetPosition;
        
        /// <summary>
        /// Progress of current movement (0.0 to 1.0)
        /// </summary>
        private float _moveProgress;

        /// <summary>
        /// Gets whether this component should respect the global pause state
        /// </summary>
        public bool ShouldPause => true;

        public override void OnAddedToEntity()
        {
            _triggerHelper = new ColliderTriggerHelper(Entity);
            
            // Ensure the entity starts properly aligned to the tile grid
            SnapToTileGrid();
        }

        public void Update()
        {
            // Check if game is paused
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            if (IsMoving)
            {
                UpdateMovement();
            }
        }

        /// <summary>
        /// Start moving the entity in the specified direction
        /// </summary>
        /// <param name="direction">Cardinal direction to move</param>
        /// <returns>True if movement started, false if blocked or already moving</returns>
        public bool StartMoving(Direction direction)
        {
            if (IsMoving)
                return false;

            var motion = GetMotionVector(direction);
            
            // Check if movement is possible
            if (!CanMove(motion))
                return false;

            // Start the movement
            _moveStartPosition = Entity.Transform.Position;
            _moveTargetPosition = _moveStartPosition + motion;
            _moveProgress = 0f;
            CurrentDirection = direction;
            IsMoving = true;

            Debug.Log($"[TileByTileMover] Started moving {direction} from {_moveStartPosition.X},{_moveStartPosition.Y} to {_moveTargetPosition.X},{_moveTargetPosition.Y}");
            return true;
        }

        /// <summary>
        /// Stop current movement immediately and snap to current tile
        /// </summary>
        public void StopMoving()
        {
            if (IsMoving)
            {
                IsMoving = false;
                CurrentDirection = null;
                SnapToTileGrid();
                Debug.Log($"[TileByTileMover] Movement stopped at {Entity.Transform.Position}");
            }
        }

        /// <summary>
        /// Update the current movement progress
        /// </summary>
        private void UpdateMovement()
        {
            if (!IsMoving)
                return;

            // Calculate movement progress based on speed and delta time
            var progressDelta = MovementSpeed * Time.DeltaTime;
            _moveProgress += progressDelta;

            if (_moveProgress >= 1.0f)
            {
                // Movement complete
                _moveProgress = 1.0f;
                Entity.Transform.Position = _moveTargetPosition;
                SnapToTileGrid();
                
                // Update triggers after reaching destination
                _triggerHelper?.Update();

                // Clear fog around the tile we just arrived at
                var tms = Core.Services.GetService<TiledMapService>();
                if (tms != null)
                {
                    var tile = GetCurrentTileCoordinates();
                    tms.ClearFogOfWarAroundTile(tile.X, tile.Y);
                }
                
                IsMoving = false;
                CurrentDirection = null;
                
                Debug.Log($"[TileByTileMover] Movement completed at {Entity.Transform.Position.X},{Entity.Transform.Position.Y}");
            }
            else
            {
                // Interpolate position
                Entity.Transform.Position = Vector2.Lerp(_moveStartPosition, _moveTargetPosition, _moveProgress);
            }
        }

        /// <summary>
        /// Check if movement in the specified direction is possible
        /// </summary>
        private bool CanMove(Vector2 motion)
        {
            var collider = Entity.GetComponent<Collider>();
            if (collider == null)
                return true;

            // Check for collisions using Nez's collision system
            CollisionResult collisionResult;
            bool isBlocked = CalculateMovement(motion, out collisionResult);
            
            if (isBlocked)
            {
                var targetPos = Entity.Transform.Position + motion;
                var targetTile = new Point((int)(targetPos.X / _tileSize), (int)(targetPos.Y / _tileSize));
                Debug.Log($"[TileByTileMover] Movement to tile ({targetTile.X},{targetTile.Y}) blocked by collision with {collisionResult.Collider?.Entity.Name ?? "unknown"}");
            }
            
            return !isBlocked;
        }

        /// <summary>
        /// Calculates movement taking collisions into account, based on Nez.Mover.CalculateMovement
        /// </summary>
        private bool CalculateMovement(Vector2 motion, out CollisionResult collisionResult)
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
        /// Snap entity position to tile grid boundaries accounting for centered collider
        /// </summary>
        public void SnapToTileGrid()
        {
            var pos = Entity.Transform.Position;
            
            // Account for the centered collider offset when calculating tile position
            var colliderCenterOffset = new Vector2(GameConfig.HeroWidth / 2f, GameConfig.HeroHeight / 2f);
            var colliderTopLeft = pos - colliderCenterOffset;
            
            // Calculate which tile the collider's top-left should be in
            var tileX = (int)System.Math.Floor(colliderTopLeft.X / _tileSize);
            var tileY = (int)System.Math.Floor(colliderTopLeft.Y / _tileSize);
            
            // Position entity so collider aligns with tile boundaries
            var tileCorner = new Vector2(tileX * _tileSize, tileY * _tileSize);
            Entity.Transform.Position = tileCorner + colliderCenterOffset;
            
            Debug.Log($"[TileByTileMover] Snapped to tile grid: ({tileX},{tileY}) at world position ({Entity.Transform.Position.X},{Entity.Transform.Position.Y})");
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
                case Direction.UpLeft:
                    return new Vector2(-_tileSize, -_tileSize);
                case Direction.UpRight:
                    return new Vector2(_tileSize, -_tileSize);
                case Direction.DownLeft:
                    return new Vector2(-_tileSize, _tileSize);
                case Direction.DownRight:
                    return new Vector2(_tileSize, _tileSize);
                default:
                    return Vector2.Zero;
            }
        }

        /// <summary>
        /// Get current tile coordinates of the entity based on collider position
        /// </summary>
        public Point GetCurrentTileCoordinates()
        {
            var pos = Entity.Transform.Position;
            var colliderCenterOffset = new Vector2(GameConfig.HeroWidth / 2f, GameConfig.HeroHeight / 2f);
            var colliderTopLeft = pos - colliderCenterOffset;
            return new Point((int)System.Math.Floor(colliderTopLeft.X / _tileSize), (int)System.Math.Floor(colliderTopLeft.Y / _tileSize));
        }
    }
}