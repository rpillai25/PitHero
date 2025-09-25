using Microsoft.Xna.Framework;
using Nez;
using System.Collections.Generic;
using PitHero.Services;
using PitHero.Util;
using Nez.Tweens;
using Nez.Tiled;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A mover that moves entities in tile-sized steps with smooth pixel interpolation using pixels/second speed.
    /// </summary>
    public class TileByTileMover : Component, IUpdatable, IPausableComponent
    {
        private ColliderTriggerHelper _triggerHelper;
        private readonly int _tileSize = GameConfig.TileSize;
        private ActorFacingComponent _facing; // facing component cache
        
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
        
        private Vector2 _moveStartPosition;
        private Vector2 _moveTargetPosition;
        private float _moveDuration;
        private float _moveElapsed;

        /// <summary>
        /// Gets whether this component should respect the global pause state
        /// </summary>
        public bool ShouldPause => true;

        public override void OnAddedToEntity()
        {
            _triggerHelper = new ColliderTriggerHelper(Entity);
            _facing = Entity.GetComponent<ActorFacingComponent>();
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

            // Update facing immediately
            if (_facing == null)
                _facing = Entity.GetComponent<ActorFacingComponent>();
            _facing?.SetFacing(direction);

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

            // Clear fog around the tile we just arrived at only for heroes
            var tms = Core.Services.GetService<TiledMapService>();
            var heroComponent = Entity.GetComponent<HeroComponent>();
            if (tms != null && heroComponent != null)
            {
                var tile = GetCurrentTileCoordinates();
                bool fogCleared = tms.ClearFogOfWarAroundTile(tile.X, tile.Y, heroComponent);
                
                // Trigger fog cooldown if fog was cleared
                if (fogCleared)
                {
                    heroComponent.TriggerFogCooldown();
                }
            }
            
            IsMoving = false;
            CurrentDirection = null;
            
            Debug.Log($"[TileByTileMover] Movement completed at {Entity.Transform.Position.X},{Entity.Transform.Position.Y}");
        }

        /// <summary>
        /// Force trigger recalculation for cases where position changes without using the mover (e.g., jumps/teleports)
        /// </summary>
        public void UpdateTriggersAfterTeleport()
        {
            _triggerHelper?.Update();
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

                // Safety net: if physics reports a collision with the tilemap but the Collision layer
                // has no tile at the target location, allow the move (stale collider or edge-touch fallback)
                if (collisionResult.Collider?.Entity != null && collisionResult.Collider.Entity.Name == "tilemap")
                {
                    var tms = Core.Services.GetService<TiledMapService>();
                    var collisionLayer = tms?.CurrentMap?.GetLayer("Collision") as TmxLayer;
                    if (collisionLayer != null)
                    {
                        var tile = collisionLayer.GetTile(targetTile.X, targetTile.Y);
                        if (tile == null)
                        {
                            Debug.Log($"[TileByTileMover] Overriding tilemap collision at ({targetTile.X},{targetTile.Y}) due to empty Collision tile");
                            return true;
                        }
                    }
                }
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

            var bounds = collider.Bounds;
            bounds.X += motion.X;
            bounds.Y += motion.Y;
            var neighbors = Physics.BoxcastBroadphaseExcludingSelf(collider, ref bounds, collider.CollidesWithLayers);

            // Iterate neighbors (HashSet) safely
            foreach (var neighbor in neighbors)
            {
                if (neighbor.IsTrigger)
                    continue;
                if (collider.CollidesWith(neighbor, motion, out CollisionResult internalCollisionResult))
                {
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