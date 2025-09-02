using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to jump into the pit when adjacent to pit boundary from outside
    /// </summary>
    public class JumpIntoPitAction : HeroActionBase
    {
        private bool _isJumping = false;
        private bool _jumpFinished = false;
        private Point _plannedTargetTile;
        
        public JumpIntoPitAction() : base(GoapConstants.JumpIntoPitAction, 1)
        {
            // Precondition: Hero and pit must be initialized
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            
            // Postcondition: Hero enters pit
            SetPostcondition(GoapConstants.InsidePit, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // If already jumping, wait for the coroutine to finish
            if (_isJumping)
            {
                if (!_jumpFinished)
                    return false; // still moving

                // Verify we actually reached the intended tile before completing
                var tileMover = hero.Entity.GetComponent<TileByTileMover>();
                var currentTile = tileMover?.GetCurrentTileCoordinates()
                    ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize),
                               (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

                if (currentTile.X != _plannedTargetTile.X || currentTile.Y != _plannedTargetTile.Y)
                {
                    Debug.Warn($"[JumpIntoPit] Jump finished flag set but hero at {currentTile.X},{currentTile.Y} not at planned target {_plannedTargetTile.X},{_plannedTargetTile.Y}. Waiting one more frame.");
                    return false;
                }

                // Movement complete, finalize the jump
                _isJumping = false;
                _jumpFinished = false;

                // Ensure triggers update so pit enter is registered
                tileMover?.UpdateTriggersAfterTeleport();

                hero.InsidePit = true;
                
                // Log hero tile position at end of execution
                var endTile = currentTile;
                Debug.Log($"[JumpIntoPit] Hero tile position at end of execution: X={endTile.X}, Y={endTile.Y}");
                Debug.Log("[JumpIntoPit] Jump completed successfully");
                return true; // Action complete
            }

            // Log hero tile position at start of execution
            var currentStartTile = hero.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates() 
                ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize), 
                           (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));
            Debug.Log($"[JumpIntoPit] Hero tile position at start of execution: X={currentStartTile.X}, Y={currentStartTile.Y}");

            var targetTile = CalculateJumpTargetTile(currentStartTile);
            if (!targetTile.HasValue)
            {
                Debug.Warn("[JumpIntoPit] Cannot calculate jump target tile");
                return true; // Action failed, but complete
            }

            _plannedTargetTile = targetTile.Value;

            // Start the coroutine-based movement to avoid TileMap collider issues
            StartJumpMovement(hero, _plannedTargetTile);
            _isJumping = true;
            _jumpFinished = false;
            
            Debug.Log($"[JumpIntoPit] Started jump to tile {_plannedTargetTile.X},{_plannedTargetTile.Y}");
            return false; // Action in progress
        }

        /// <summary>
        /// Calculate the target tile based on current position - always jump from right side
        /// </summary>
        private Point? CalculateJumpTargetTile(Point currentTile)
        {
            // We'll always jump into the pit from the right, so target is 2 tiles to the left
            return new Point(currentTile.X - 2, currentTile.Y);
        }

        /// <summary>
        /// Start coroutine-based movement to target tile
        /// </summary>
        private void StartJumpMovement(HeroComponent hero, Point targetTile)
        {
            var targetPosition = TileToWorldPosition(targetTile);
            var entity = hero.Entity;
            
            // Start the movement coroutine
            Core.StartCoroutine(JumpMovementCoroutine(entity, targetPosition, GameConfig.HeroJumpSpeed));
        }

        /// <summary>
        /// Coroutine that smoothly moves the entity to target position over time
        /// </summary>
        private System.Collections.IEnumerator JumpMovementCoroutine(Entity entity, Vector2 targetPosition, float tilesPerSecond)
        {
            var startPosition = entity.Transform.Position;
            var distance = Vector2.Distance(startPosition, targetPosition);
            var duration = distance / (tilesPerSecond * GameConfig.TileSize); // Convert tiles per second to pixels per second
            
            // Start jump animation - determine direction from start to target
            var jumpDirection = GetJumpDirection(startPosition, targetPosition);
            var jumpAnimComponent = entity.GetComponent<HeroJumpAnimationComponent>();
            if (jumpAnimComponent != null)
            {
                jumpAnimComponent.StartJump(jumpDirection, duration);
            }
            
            var elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.DeltaTime;
                var progress = elapsed / duration;
                
                // Smooth interpolation
                entity.Transform.Position = Vector2.Lerp(startPosition, targetPosition, progress);
                
                yield return null; // Wait for next frame
            }
            
            // Ensure we end exactly at target
            entity.Transform.Position = targetPosition;
            
            // End jump animation
            if (jumpAnimComponent != null)
            {
                jumpAnimComponent.EndJump();
            }
            
            // Snap to tile grid for precision
            var tileMover = entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
                // Force trigger update so the pit enter trigger updates immediately
                tileMover.UpdateTriggersAfterTeleport();
            }

            var tiledMapService = Core.Services.GetService<TiledMapService>();
            tiledMapService?.ClearFogOfWarAroundTile(
                (int)(targetPosition.X / GameConfig.TileSize),
                (int)(targetPosition.Y / GameConfig.TileSize)
            );

            _jumpFinished = true;
            Debug.Log($"[JumpIntoPit] Jump movement completed at {entity.Transform.Position.X},{entity.Transform.Position.Y}");
        }

        /// <summary>
        /// Determine jump direction based on start and target positions
        /// </summary>
        private Direction GetJumpDirection(Vector2 startPosition, Vector2 targetPosition)
        {
            var delta = targetPosition - startPosition;
            
            // Since we're jumping into the pit from the right, this should be left
            if (System.Math.Abs(delta.X) > System.Math.Abs(delta.Y))
            {
                return delta.X > 0 ? Direction.Right : Direction.Left;
            }
            else
            {
                return delta.Y > 0 ? Direction.Down : Direction.Up;
            }
        }
    }
}