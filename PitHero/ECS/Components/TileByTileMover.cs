using Microsoft.Xna.Framework;
using Nez;
using System.Collections.Generic;
using PitHero.Services;
using PitHero.Util;
using Nez.Tweens;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A mover that moves entities in tile-sized steps with smooth pixel interpolation using pixels/second speed.
    /// </summary>
    public class TileByTileMover : Component, IUpdatable, IPausableComponent
    {
        private ColliderTriggerHelper _triggerHelper;
        private readonly int _tileSize = GameConfig.TileSize;
        
        /// <summary>
        /// Movement speed in pixels per second
        /// </summary>
        public float MovementSpeed { get; set; } = GameConfig.HeroMovementSpeed;
        
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
        /// Duration of current move (seconds) computed from MovementSpeed (pixels/sec)
        /// </summary>
        private float _moveDuration;
        
        /// <summary>
        /// Elapsed time since movement started (seconds)
        /// </summary>
        private float _moveElapsed;

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
            _moveElapsed = 0f;
            CurrentDirection = direction;
            IsMoving = true;

            // Calculate duration using pixels-per-second speed
            var distancePixels = motion.Length();
            _moveDuration = MovementSpeed > 0f ? distancePixels / MovementSpeed : 0f;

            Debug.Log($"[TileByTileMover] Started moving {direction} from {_moveStartPosition.X},{_moveStartPosition.Y} to {_moveTargetPosition.X},{_moveTargetPosition.Y} with duration {_moveDuration}");
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
                Debug.Log($"[TileByTileMover] Movement stopped at {Entity.Transform.Position.X},{Entity.Transform.Position.Y}");
            }
        }

        /// <summary>
        /// Update the current movement progress using elapsed/duration for smooth pixel interpolation
        /// </summary>
        private void UpdateMovement()
        {
            if (!IsMoving)
                return;

            if (_moveDuration <= 0f)
            {
                // No duration means instant move
                Entity.Transform.Position = _moveTargetPosition;
                CompleteMove();
                return;
            }

            _moveElapsed += Time.DeltaTime;
            var progress = _moveElapsed / _moveDuration;

            if (progress >= 1f)
            {
                // Ensure we land exactly at target
                Entity.Transform.Position = _moveTargetPosition;
                CompleteMove();
            }
            else
            {
                // Interpolate smoothly between start and target
                Entity.Transform.Position = Vector2.Lerp(_moveStartPosition, _moveTargetPosition, progress);
            }
        }

        /// <summary>
        /// Finalize a move: snap to grid, update triggers, clear fog, reset state
        /// </summary>
        private void CompleteMove()
        {
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