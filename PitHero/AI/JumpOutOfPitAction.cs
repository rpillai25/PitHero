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
        /// Find the best pit exit direction by looking for nearest perimeter tile with clear path to explored exit
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
            
            Point? bestTarget = null;
            float bestDistance = float.MaxValue;

            // Check each wall perimeter for the best exit point
            var candidates = new[]
            {
                CheckNorthWallPerimeter(hero, currentTile, pitBounds, tiledMapService),
                CheckWestWallPerimeter(hero, currentTile, pitBounds, tiledMapService),
                CheckSouthWallPerimeter(hero, currentTile, pitBounds, tiledMapService),
                CheckEastWallPerimeter(hero, currentTile, pitBounds, tiledMapService)
            };

            foreach (var candidate in candidates)
            {
                if (!candidate.HasValue) continue;

                var distance = Vector2.Distance(
                    new Vector2(currentTile.X, currentTile.Y),
                    new Vector2(candidate.Value.X, candidate.Value.Y)
                );

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidate.Value;
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
        /// Check north wall perimeter for the best exit point
        /// </summary>
        private Point? CheckNorthWallPerimeter(HeroComponent hero, Point currentTile, Rectangle pitBounds, TiledMapService tiledMapService)
        {
            var northWallRow = pitBounds.Y + 1; // Inner perimeter north wall row
            var startCol = pitBounds.X + 1; // Inner perimeter start column
            var endCol = pitBounds.Right - 2; // Inner perimeter end column

            return CheckWallPerimeter(hero, currentTile, tiledMapService, "North", northWallRow, startCol, endCol, true, new Point(0, -2));
        }

        /// <summary>
        /// Check west wall perimeter for the best exit point
        /// </summary>
        private Point? CheckWestWallPerimeter(HeroComponent hero, Point currentTile, Rectangle pitBounds, TiledMapService tiledMapService)
        {
            var westWallCol = pitBounds.X + 1; // Inner perimeter west wall column
            var startRow = pitBounds.Y + 1; // Inner perimeter start row
            var endRow = pitBounds.Bottom - 2; // Inner perimeter end row

            return CheckWallPerimeter(hero, currentTile, tiledMapService, "West", westWallCol, startRow, endRow, false, new Point(-2, 0));
        }

        /// <summary>
        /// Check south wall perimeter for the best exit point
        /// </summary>
        private Point? CheckSouthWallPerimeter(HeroComponent hero, Point currentTile, Rectangle pitBounds, TiledMapService tiledMapService)
        {
            var southWallRow = pitBounds.Bottom - 2; // Inner perimeter south wall row
            var startCol = pitBounds.X + 1; // Inner perimeter start column
            var endCol = pitBounds.Right - 2; // Inner perimeter end column

            return CheckWallPerimeter(hero, currentTile, tiledMapService, "South", southWallRow, startCol, endCol, true, new Point(0, 2));
        }

        /// <summary>
        /// Check east wall perimeter for the best exit point
        /// </summary>
        private Point? CheckEastWallPerimeter(HeroComponent hero, Point currentTile, Rectangle pitBounds, TiledMapService tiledMapService)
        {
            var eastWallCol = pitBounds.Right - 2; // Inner perimeter east wall column
            var startRow = pitBounds.Y + 1; // Inner perimeter start row
            var endRow = pitBounds.Bottom - 2; // Inner perimeter end row

            return CheckWallPerimeter(hero, currentTile, tiledMapService, "East", eastWallCol, startRow, endRow, false, new Point(2, 0));
        }

        /// <summary>
        /// Generic method to check a wall perimeter and find the closest valid exit point
        /// </summary>
        private Point? CheckWallPerimeter(HeroComponent hero, Point currentTile, TiledMapService tiledMapService, 
            string wallName, int fixedCoord, int startVar, int endVar, bool isHorizontalWall, Point jumpDelta)
        {
            Point? bestPerimeterTile = null;
            float bestDistance = float.MaxValue;

            for (int varCoord = startVar; varCoord <= endVar; varCoord++)
            {
                // Determine perimeter tile position
                var perimeterTile = isHorizontalWall 
                    ? new Point(varCoord, fixedCoord)  // Horizontal wall: varying X, fixed Y
                    : new Point(fixedCoord, varCoord); // Vertical wall: fixed X, varying Y

                // Calculate jump target (2 tiles in the direction)
                var jumpTarget = new Point(perimeterTile.X + jumpDelta.X, perimeterTile.Y + jumpDelta.Y);

                // Check if jump target is explored (no fog of war)
                if (tiledMapService.HasFogOfWar(jumpTarget.X, jumpTarget.Y))
                {
                    continue;
                }

                // Check if jump target is passable
                if (!hero.IsPassable(jumpTarget))
                {
                    continue;
                }

                // Check if there's a clear path from current position to perimeter tile
                var pathToPerimeter = hero.CalculatePath(currentTile, perimeterTile);
                if (pathToPerimeter == null || pathToPerimeter.Count == 0)
                {
                    continue;
                }

                // Calculate distance to this perimeter tile
                var distance = Vector2.Distance(
                    new Vector2(currentTile.X, currentTile.Y),
                    new Vector2(perimeterTile.X, perimeterTile.Y)
                );

                Debug.Log($"[JumpOutOfPit] {wallName} wall: perimeter tile ({perimeterTile.X},{perimeterTile.Y}) -> jump target ({jumpTarget.X},{jumpTarget.Y}), distance: {distance}");

                // Keep the closest valid perimeter tile
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPerimeterTile = perimeterTile;
                }
            }

            if (bestPerimeterTile.HasValue)
            {
                var jumpTarget = new Point(bestPerimeterTile.Value.X + jumpDelta.X, bestPerimeterTile.Value.Y + jumpDelta.Y);
                Debug.Log($"[JumpOutOfPit] {wallName} wall best perimeter tile: ({bestPerimeterTile.Value.X},{bestPerimeterTile.Value.Y}) -> target ({jumpTarget.X},{jumpTarget.Y})");
                return jumpTarget; // Return the jump target, not the perimeter tile
            }

            return null;
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