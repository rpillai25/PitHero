using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;
using System.Collections;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to jump out of the pit when ready to jump out
    /// This is the reverse of JumpIntoPit - hero jumps from inside boundary to outside
    /// </summary>
    public class JumpOutOfPitAction : HeroActionBase
    {
        private bool _isJumping = false;
        private bool _jumpFinished = false;
        private Point _plannedTargetTile;
        
        public JumpOutOfPitAction() : base(GoapConstants.JumpOutOfPitAction, 1)
        {
            // Preconditions: Hero must be inside pit and wizard orb activated
            SetPrecondition(GoapConstants.InsidePit, true);
            SetPrecondition(GoapConstants.ActivatedWizardOrb, true);
            
            // Postcondition: Hero is outside pit
            SetPostcondition(GoapConstants.OutsidePit, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // If already jumping, check if movement is complete
            if (_isJumping)
            {
                if (!_jumpFinished)
                {
                    return false; // Still moving, action not complete
                }

                // Verify we actually reached the intended tile before completing
                var tileMover = hero.Entity.GetComponent<TileByTileMover>();
                var currentTile = tileMover?.GetCurrentTileCoordinates() 
                    ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize), 
                               (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

                if (currentTile.X != _plannedTargetTile.X || currentTile.Y != _plannedTargetTile.Y)
                {
                    Debug.Warn($"[JumpOutOfPit] Jump finished flag set but hero at {currentTile.X},{currentTile.Y} not at planned target {_plannedTargetTile.X},{_plannedTargetTile.Y}. Waiting one more frame.");
                    return false;
                }
                
                // Movement complete, finalize the jump
                _isJumping = false;
                _jumpFinished = false;

                // Ensure triggers update so pit exit is registered
                tileMover?.UpdateTriggersAfterTeleport();

                hero.InsidePit = false;  // Set InsidePit = False according to specification
                
                Debug.Log("[JumpOutOfPit] Jump out completed successfully");
                return true; // Action complete
            }

            // Start the jump out
            var targetTile = CalculateJumpOutTargetTile(hero);
            if (!targetTile.HasValue)
            {
                Debug.Warn("[JumpOutOfPit] Cannot calculate jump out target tile");
                return true; // Action failed, but complete
            }

            _plannedTargetTile = targetTile.Value;

            // Start the coroutine-based movement to avoid TileMap collider issues
            StartJumpOutMovement(hero, _plannedTargetTile);
            _isJumping = true;
            _jumpFinished = false;
            
            Debug.Log($"[JumpOutOfPit] Started jump out to tile {_plannedTargetTile.X},{_plannedTargetTile.Y}");
            return false; // Action in progress
        }

        /// <summary>
        /// Calculate the target tile for jumping out - looks for nearest explored spot with clear path, falls back to east
        /// </summary>
        private Point? CalculateJumpOutTargetTile(HeroComponent hero)
        {
            var currentTile = hero.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates() 
                ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize), 
                           (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            // First try to find the best exit direction based on explored areas
            var bestExitTarget = FindBestPitExitDirection(hero, currentTile);
            if (bestExitTarget.HasValue)
            {
                Debug.Log($"[JumpOutOfPit] Found intelligent exit route from {currentTile.X},{currentTile.Y} to {bestExitTarget.Value.X},{bestExitTarget.Value.Y}");
                return bestExitTarget.Value;
            }

            // Fallback to default behavior: Jump out 2 tiles to the right (reverse of jumping in from the right)
            var targetTile = new Point(currentTile.X + 2, currentTile.Y);
            
            Debug.Log($"[JumpOutOfPit] Using fallback exit route from {currentTile.X},{currentTile.Y} to {targetTile.X},{targetTile.Y}");
            return targetTile;
        }

        /// <summary>
        /// Find the best pit exit direction by looking for nearest explored spot with clear path
        /// </summary>
        private Point? FindBestPitExitDirection(HeroComponent hero, Point currentTile)
        {
            // Get services we need
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            
            if (tiledMapService == null || !hero.IsPathfindingInitialized)
            {
                Debug.Warn("[JumpOutOfPit] Cannot find intelligent exit - missing services or pathfinding not initialized");
                return null;
            }

            // Get current pit bounds (dynamic)
            var pitBounds = GetCurrentPitBounds(pitWidthManager);
            Debug.Log($"[JumpOutOfPit] Checking pit bounds: X={pitBounds.X}, Y={pitBounds.Y}, Width={pitBounds.Width}, Height={pitBounds.Height}");
            
            // Define the four cardinal directions to check
            var directions = new[]
            {
                new { Name = "East", Delta = new Point(2, 0) },   // Right (original behavior)
                new { Name = "North", Delta = new Point(0, -2) }, // Up
                new { Name = "West", Delta = new Point(-2, 0) },  // Left
                new { Name = "South", Delta = new Point(0, 2) }   // Down
            };

            Point? bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (var direction in directions)
            {
                var candidateTarget = new Point(currentTile.X + direction.Delta.X, currentTile.Y + direction.Delta.Y);
                
                // Check if this target is outside the pit
                if (!IsPointOutsidePit(candidateTarget, pitBounds))
                    continue;

                // Check if the target area is explored (no fog of war)
                if (tiledMapService.HasFogOfWar(candidateTarget.X, candidateTarget.Y))
                {
                    Debug.Log($"[JumpOutOfPit] {direction.Name} direction target ({candidateTarget.X},{candidateTarget.Y}) has fog of war - skipping");
                    continue;
                }

                // Check if target is passable
                if (!hero.IsPassable(candidateTarget))
                {
                    Debug.Log($"[JumpOutOfPit] {direction.Name} direction target ({candidateTarget.X},{candidateTarget.Y}) is not passable - skipping");
                    continue;
                }

                // Check if there's a clear path to the target
                var path = hero.CalculatePath(currentTile, candidateTarget);
                if (path == null || path.Count == 0)
                {
                    Debug.Log($"[JumpOutOfPit] {direction.Name} direction target ({candidateTarget.X},{candidateTarget.Y}) has no path - skipping");
                    continue;
                }

                // Calculate distance to this candidate
                var distance = Vector2.Distance(
                    new Vector2(currentTile.X, currentTile.Y),
                    new Vector2(candidateTarget.X, candidateTarget.Y)
                );

                Debug.Log($"[JumpOutOfPit] {direction.Name} direction is valid - target ({candidateTarget.X},{candidateTarget.Y}), distance: {distance}");

                // Keep the closest valid target
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidateTarget;
                }
            }

            if (bestTarget.HasValue)
            {
                Debug.Log($"[JumpOutOfPit] Best exit direction found: target ({bestTarget.Value.X},{bestTarget.Value.Y}) at distance {bestDistance}");
            }
            else
            {
                Debug.Log("[JumpOutOfPit] No valid intelligent exit direction found");
            }

            return bestTarget;
        }

        /// <summary>
        /// Get current pit bounds, accounting for dynamic width
        /// </summary>
        private Rectangle GetCurrentPitBounds(PitWidthManager pitWidthManager)
        {
            if (pitWidthManager != null)
            {
                var width = pitWidthManager.CurrentPitRectWidthTiles;
                return new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY, width, GameConfig.PitRectHeight);
            }
            
            // Fallback to static bounds
            return new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY, GameConfig.PitRectWidth, GameConfig.PitRectHeight);
        }

        /// <summary>
        /// Check if a point is outside the pit boundaries
        /// </summary>
        private bool IsPointOutsidePit(Point point, Rectangle pitBounds)
        {
            return point.X < pitBounds.X || point.X >= pitBounds.Right ||
                   point.Y < pitBounds.Y || point.Y >= pitBounds.Bottom;
        }

        /// <summary>
        /// Start coroutine-based movement to target tile
        /// </summary>
        private void StartJumpOutMovement(HeroComponent hero, Point targetTile)
        {
            var targetPosition = TileToWorldPosition(targetTile);
            var entity = hero.Entity;
            
            // Start the movement coroutine
            Core.StartCoroutine(JumpOutMovementCoroutine(entity, targetPosition, GameConfig.HeroJumpSpeed));
        }

        /// <summary>
        /// Coroutine that smoothly moves the entity to target position over time
        /// </summary>
        private IEnumerator JumpOutMovementCoroutine(Entity entity, Vector2 targetPosition, float tilesPerSecond)
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
                // Force trigger update so the pit exit trigger updates immediately
                tileMover.UpdateTriggersAfterTeleport();
            }

            // Clear fog of war around the landing position
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            tiledMapService?.ClearFogOfWarAroundTile(
                (int)(targetPosition.X / GameConfig.TileSize),
                (int)(targetPosition.Y / GameConfig.TileSize)
            );

            _jumpFinished = true;

            Debug.Log($"[JumpOutOfPit] Jump out movement completed at {entity.Transform.Position.X},{entity.Transform.Position.Y}");
        }

        /// <summary>
        /// Determine jump direction based on start and target positions
        /// </summary>
        private Direction GetJumpDirection(Vector2 startPosition, Vector2 targetPosition)
        {
            var delta = targetPosition - startPosition;
            
            // Since we're jumping out of the pit to the right, this should be right
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