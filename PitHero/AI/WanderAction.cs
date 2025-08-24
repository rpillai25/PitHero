using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.Pathfinding;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to explore the pit by moving to the nearest unknown tile
    /// </summary>
    public class WanderAction : HeroActionBase
    {
        private List<Point> _currentPath;
        private int _pathIndex;
        private Point _targetTile;
        private bool _hasSelectedTarget;

        // Track multiple failed targets to avoid reselect loops
        private HashSet<Point> _failedTargets;
        
        // Track consecutive failures to current target
        private int _consecutiveFailures;
        private const int MAX_CONSECUTIVE_FAILURES = 3;

        public WanderAction() : base(GoapConstants.WanderAction, 1)
        {
            SetPrecondition(GoapConstants.EnteredPit, true);
            SetPostcondition(GoapConstants.MapExplored, true);
            _failedTargets = new HashSet<Point>(8); // Pre-allocate small capacity
        }

        /// <summary>
        /// Execute the action each frame; moves along path toward nearest unknown tile
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover == null)
            {
                Debug.Warn("WanderAction: Hero entity missing TileByTileMover component");
                ResetInternal();
                return true;
            }

            // Select a target if we haven't yet for this execution
            if (!_hasSelectedTarget)
            {
                var heroTile = tileMover.GetCurrentTileCoordinates();
                var nearestUnknownTile = FindNearestUnknownTile(hero, heroTile);
                if (!nearestUnknownTile.HasValue)
                {
                    Debug.Log("[Wander] No unknown tiles found - exploration complete");
                    ResetInternal();
                    return true; // All done
                }

                _targetTile = nearestUnknownTile.Value;
                _hasSelectedTarget = true;
                _consecutiveFailures = 0; // Reset failure count for new target
                Debug.Log($"[Wander] Selected target tile {_targetTile.X},{_targetTile.Y}");
            }

            // Get current tile position
            var currentTile = tileMover.GetCurrentTileCoordinates();

            // Check if we've reached the target
            if (currentTile.X == _targetTile.X && currentTile.Y == _targetTile.Y)
            {
                Debug.Log($"[Wander] Reached target tile {_targetTile.X},{_targetTile.Y}");

                // Clear fog of war around this tile
                var tiledMapService = Core.Services.GetService<TiledMapService>();
                if (tiledMapService != null)
                {
                    tiledMapService.ClearFogOfWarAroundTile(currentTile.X, currentTile.Y);
                }

                // Successfully reached target, remove it from failed targets if it was there
                _failedTargets.Remove(_targetTile);

                // Prepare to pick a new target next tick. Keep action running.
                ResetInternal();
                return false; // continue exploring
            }

            // If we don't have a path yet, calculate one
            if (_currentPath == null || _currentPath.Count == 0)
            {
                _currentPath = CalculatePathToTarget(hero, currentTile, _targetTile);
                _pathIndex = 0;

                if (_currentPath == null || _currentPath.Count == 0)
                {
                    Debug.Warn($"[Wander] Could not find path from {currentTile.X},{currentTile.Y} to {_targetTile.X},{_targetTile.Y}");
                    
                    _consecutiveFailures++;
                    Debug.Log($"[Wander] Consecutive failures for target {_targetTile.X},{_targetTile.Y}: {_consecutiveFailures}");
                    
                    if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    {
                        Debug.Log($"[Wander] Target {_targetTile.X},{_targetTile.Y} failed {MAX_CONSECUTIVE_FAILURES} times, marking as unreachable and selecting new target");
                        // Add to failed targets set to avoid future reselection
                        _failedTargets.Add(_targetTile);
                        
                        // Force selection of new target
                        ResetInternal();
                    }

                    return false; // try again next tick (either with new target or retry current)
                }

                Debug.Log($"[Wander] Calculated path with {_currentPath.Count} steps");
            }

            // If not currently moving, start moving to the next tile in the path
            if (!tileMover.IsMoving && _pathIndex < _currentPath.Count)
            {
                var nextTile = _currentPath[_pathIndex];
                var direction = GetDirectionToTile(currentTile, nextTile);

                if (direction.HasValue)
                {
                    Debug.Log($"[Wander] Moving {direction.Value} to tile {nextTile.X},{nextTile.Y}");
                    var moveStarted = tileMover.StartMoving(direction.Value);
                    if (moveStarted)
                    {
                        _pathIndex++;
                        // Reset failure count on successful movement
                        _consecutiveFailures = 0;
                    }
                    else
                    {
                        Debug.Log("[Wander] Movement blocked, recalculating path");
                        _consecutiveFailures++;
                        Debug.Log($"[Wander] Consecutive failures for target {_targetTile.X},{_targetTile.Y}: {_consecutiveFailures}");
                        
                        if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                        {
                            Debug.Log($"[Wander] Movement to target {_targetTile.X},{_targetTile.Y} blocked {MAX_CONSECUTIVE_FAILURES} times, selecting new target");
                            // Add to failed targets set to avoid future reselection
                            _failedTargets.Add(_targetTile);
                            
                            // Force selection of new target
                            ResetInternal();
                        }
                        else
                        {
                            // Recalculate path next frame for same target
                            _currentPath = null;
                        }
                    }
                }
                else
                {
                    Debug.Warn($"[Wander] Invalid movement from {currentTile.X},{currentTile.Y} to {nextTile.X},{nextTile.Y}");
                    _consecutiveFailures++;
                    
                    if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    {
                        Debug.Log($"[Wander] Invalid movement to target {_targetTile.X},{_targetTile.Y} failed {MAX_CONSECUTIVE_FAILURES} times, selecting new target");
                        _failedTargets.Add(_targetTile);
                        ResetInternal();
                    }
                    
                    return false; // try again next tick
                }
            }

            return false; // Action still in progress
        }

        /// <summary>
        /// Find the nearest tile that is still covered by fog of war and passable
        /// </summary>
        private Point? FindNearestUnknownTile(HeroComponent hero, Point heroTile)
        {
            var tms = Core.Services.GetService<TiledMapService>();
            if (tms?.CurrentMap == null)
            {
                Debug.Warn("[Wander] No tilemap service available");
                return null;
            }

            var fogLayer = tms.CurrentMap.GetLayer<TmxLayer>("FogOfWar");
            if (fogLayer == null)
            {
                Debug.Warn("[Wander] No FogOfWar layer found");
                return null;
            }

            Point? nearestUnknownTile = null;
            float shortestDistance = float.MaxValue;

            // Get pit bounds from PitWidthManager
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            int pitMinX, pitMinY, pitMaxX, pitMaxY;
            
            if (pitWidthManager != null)
            {
                // The explorable area is the inner area, excluding walls
                // Left wall is at PitRectX (x=1), so explorable starts at PitRectX + 1 (x=2)
                // Right wall is at CurrentPitRightEdge - 1, so explorable ends at CurrentPitRightEdge - 2
                pitMinX = GameConfig.PitRectX + 1; // x=2 (first explorable column)
                pitMinY = GameConfig.PitRectY + 1; // y=3 (first explorable row)
                pitMaxX = pitWidthManager.CurrentPitRightEdge - 2; // Last explorable column (before inner wall)
                pitMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // y=9 (last explorable row)
                Debug.Log($"[Wander] Using dynamic pit bounds: explorable area ({pitMinX},{pitMinY}) to ({pitMaxX},{pitMaxY}), pit right edge={pitWidthManager.CurrentPitRightEdge}");
            }
            else
            {
                // Use default pit bounds
                pitMinX = GameConfig.PitRectX + 1; // 2
                pitMinY = GameConfig.PitRectY + 1; // 3
                pitMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 2; // 11
                pitMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
                Debug.Log($"[Wander] Using default pit bounds: explorable area ({pitMinX},{pitMinY}) to ({pitMaxX},{pitMaxY})");
            }

            int fogTileCount = 0;
            int passableFogTileCount = 0;
            int skippedFailedCount = 0;

            // Scan the pit area for tiles still covered by fog
            for (int x = pitMinX; x <= pitMaxX; x++)
            {
                for (int y = pitMinY; y <= pitMaxY; y++)
                {
                    // Check if this tile is within bounds and has fog
                    if (x >= 0 && y >= 0 && x < fogLayer.Width && y < fogLayer.Height)
                    {
                        var tile = fogLayer.GetTile(x, y);
                        if (tile != null) // Tile has fog (unknown)
                        {
                            fogTileCount++;
                            
                            // Skip failed targets to avoid loops
                            var tilePoint = new Point(x, y);
                            if (_failedTargets.Contains(tilePoint))
                            {
                                skippedFailedCount++;
                                continue;
                            }

                            // Check if tile is passable using hero's pathfinding component
                            bool isPassable = true;
                            if (hero.IsPathfindingInitialized)
                            {
                                isPassable = hero.IsPassable(new Point(x, y));
                            }
                            
                            if (!isPassable)
                            {
                                Debug.Log($"[Wander] Tile ({x},{y}) has fog but is not passable");
                                continue;
                            }
                            
                            passableFogTileCount++;

                            // Calculate distance from hero to this tile
                            var distance = Vector2.Distance(
                                new Vector2(heroTile.X, heroTile.Y),
                                new Vector2(x, y)
                            );

                            if (distance < shortestDistance)
                            {
                                shortestDistance = distance;
                                nearestUnknownTile = new Point(x, y);
                            }
                        }
                    }
                }
            }

            Debug.Log($"[Wander] Found {fogTileCount} fog tiles total, {passableFogTileCount} passable fog tiles, skipped {skippedFailedCount} failed targets");

            if (nearestUnknownTile.HasValue)
            {
                Debug.Log($"[Wander] Found nearest unknown tile at {nearestUnknownTile.Value.X},{nearestUnknownTile.Value.Y} distance {shortestDistance}");
            }
            else
            {
                Debug.Log("[Wander] No unknown tiles found in pit area");
                // If we have failed targets and no valid targets, clear failed targets and try again
                if (_failedTargets.Count > 0)
                {
                    Debug.Log($"[Wander] Clearing {_failedTargets.Count} failed targets to retry exploration");
                    _failedTargets.Clear();
                    // Return null to trigger retry on next frame
                    return null;
                }
            }

            return nearestUnknownTile;
        }

        /// <summary>
        /// Calculate AStar path from current position to target tile
        /// </summary>
        private List<Point> CalculatePathToTarget(HeroComponent hero, Point start, Point target)
        {
            try
            {
                // Use the hero's pathfinding component instead of global service
                if (!hero.IsPathfindingInitialized)
                {
                    Debug.Warn("[Wander] Hero pathfinding not initialized");
                    return null;
                }

                Debug.Log($"[Wander] Calculating path from {start.X},{start.Y} to {target.X},{target.Y}");
                var path = hero.CalculatePath(start, target);
                
                if (path != null && path.Count > 0)
                {
                    Debug.Log($"[Wander] Found path with {path.Count} steps");
                }
                else
                {
                    Debug.Warn($"[Wander] No path found from {start.X},{start.Y} to {target.X},{target.Y}");
                }
                
                return path;
            }
            catch (System.Exception ex)
            {
                Debug.Error($"[Wander] Error calculating path: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get direction from current tile to target tile (assumes adjacent tiles)
        /// </summary>
        private Direction? GetDirectionToTile(Point from, Point to)
        {
            var deltaX = to.X - from.X;
            var deltaY = to.Y - from.Y;
            
            if (deltaX == 1 && deltaY == 0) return Direction.Right;
            if (deltaX == -1 && deltaY == 0) return Direction.Left;
            if (deltaX == 0 && deltaY == 1) return Direction.Down;
            if (deltaX == 0 && deltaY == -1) return Direction.Up;
            
            Debug.Warn($"[Wander] Non-adjacent tiles: from {from.X},{from.Y} to {to.X},{to.Y}");
            return null;
        }

        /// <summary>
        /// Helper method to get tile coordinates from world position
        /// </summary>
        private Point GetTileCoordinates(Vector2 worldPosition, int tileSize)
        {
            return new Point((int)(worldPosition.X / tileSize), (int)(worldPosition.Y / tileSize));
        }

        /// <summary>
        /// Reset internal execution state so future runs can reselect target
        /// </summary>
        private void ResetInternal()
        {
            _currentPath = null;
            _pathIndex = 0;
            _hasSelectedTarget = false;
            _consecutiveFailures = 0;
            // Keep _failedTargets to avoid reselecting known unreachable targets
        }
    }
}