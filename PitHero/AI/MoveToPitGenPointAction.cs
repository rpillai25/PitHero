using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that moves the hero to the pit generation point at (34, 6)
    /// Once the hero reaches this position, the queued pit level will be regenerated
    /// </summary>
    public class MoveToPitGenPointAction : HeroActionBase
    {
        private static readonly Point PIT_GEN_POINT = new Point(34, 6);
        
        private List<Point> _currentPath;
        private int _pathIndex;
        
        public MoveToPitGenPointAction() : base(GoapConstants.MoveToPitGenPointAction, 1)
        {
            // Precondition: Hero should be moving to pit gen point
            SetPrecondition(GoapConstants.MovingToPitGenPoint, true);
            
            // Postconditions: Hero will be at pit gen point and pit will be initialized
            SetPostcondition(GoapConstants.AtPitGenPoint, true);
            SetPostcondition(GoapConstants.PitInitialized, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // Get the TileByTileMover component from the hero entity
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            
            if (tileMover == null)
            {
                Debug.Warn("MoveToPitGenPointAction: Hero entity missing TileByTileMover component");
                ResetInternal();
                return true; // complete as failed
            }

            // Check if we're already at the pit gen point
            var currentTile = tileMover.GetCurrentTileCoordinates();
            if (currentTile.X == PIT_GEN_POINT.X && currentTile.Y == PIT_GEN_POINT.Y)
            {
                Debug.Log($"[MoveToPitGenPoint] Reached pit generation point at tile {PIT_GEN_POINT.X},{PIT_GEN_POINT.Y}");
                
                // Regenerate the pit with the queued level
                RegeneratePitAtGenPoint(hero);
                
                // Clear movement flag after reaching destination
                hero.MovingToPitGenPoint = false;
                
                ResetInternal();
                return true;
            }

            // If we don't have a path yet, calculate one
            if (_currentPath == null || _currentPath.Count == 0)
            {
                _currentPath = CalculatePathToTarget(hero, currentTile, PIT_GEN_POINT);
                _pathIndex = 0;
                
                if (_currentPath == null || _currentPath.Count == 0)
                {
                    Debug.Warn($"[MoveToPitGenPoint] Could not find path from {currentTile.X},{currentTile.Y} to {PIT_GEN_POINT.X},{PIT_GEN_POINT.Y}");
                    ResetInternal();
                    return true; // Complete the action as failed
                }
                
                Debug.Log($"[MoveToPitGenPoint] Calculated path with {_currentPath.Count} steps");
            }

            // If not currently moving, start moving to the next tile in the path
            if (!tileMover.IsMoving)
            {
                if (_pathIndex < _currentPath.Count)
                {
                    var nextTile = _currentPath[_pathIndex];
                    var currentPos = tileMover.GetCurrentTileCoordinates();
                    
                    // Determine direction to next tile
                    var direction = GetDirectionToTile(currentPos, nextTile);
                    
                    if (direction.HasValue)
                    {
                        bool moveStarted = tileMover.StartMoving(direction.Value);
                        
                        if (moveStarted)
                        {
                            _pathIndex++;
                            Debug.Log($"[MoveToPitGenPoint] Moving to tile {nextTile.X},{nextTile.Y} (step {_pathIndex}/{_currentPath.Count})");
                        }
                        else
                        {
                            Debug.Log("[MoveToPitGenPoint] Movement blocked, recalculating path");
                            _currentPath = null; // Force recalculation next frame
                        }
                    }
                    else
                    {
                        Debug.Warn($"[MoveToPitGenPoint] Could not determine direction from {currentPos.X},{currentPos.Y} to {nextTile.X},{nextTile.Y}");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
                else
                {
                    // Reached end of path, check if we're at target
                    if (currentTile.X == PIT_GEN_POINT.X && currentTile.Y == PIT_GEN_POINT.Y)
                    {
                        Debug.Log($"[MoveToPitGenPoint] Reached pit generation point at tile {PIT_GEN_POINT.X},{PIT_GEN_POINT.Y}");
                        
                        // Regenerate the pit with the queued level
                        RegeneratePitAtGenPoint(hero);
                        
                        // Clear movement flag after reaching destination
                        hero.MovingToPitGenPoint = false;
                        
                        ResetInternal();
                        return true;
                    }
                    else
                    {
                        Debug.Log("[MoveToPitGenPoint] Path completed but not at target, recalculating");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
            }
            
            // Action continues as long as we're moving towards the pit gen point
            return false;
        }

        /// <summary>
        /// Regenerate the pit with the queued level when hero reaches gen point
        /// </summary>
        private void RegeneratePitAtGenPoint(HeroComponent hero)
        {
            Debug.Log("[MoveToPitGenPoint] Starting pit regeneration at gen point");
            
            // Get the queued pit level
            var queueService = Core.Services.GetService<PitLevelQueueService>();
            var queuedLevel = queueService?.DequeueLevel();
            
            if (queuedLevel.HasValue)
            {
                Debug.Log($"[MoveToPitGenPoint] Regenerating pit with queued level {queuedLevel.Value}");
                
                // Set the pit level using PitWidthManager
                var pitWidthManager = Core.Services.GetService<PitWidthManager>();
                if (pitWidthManager != null)
                {
                    pitWidthManager.ReinitRightEdge(); // Reset to initial state
                    pitWidthManager.SetPitLevel(queuedLevel.Value);
                    
                    // Mark pit as initialized for GOAP
                    hero.PitInitialized = true;
                    
                    Debug.Log($"[MoveToPitGenPoint] Pit regenerated with level {queuedLevel.Value}, right edge at x={pitWidthManager.CurrentPitRightEdge}");
                }
                else
                {
                    Debug.Error("[MoveToPitGenPoint] PitWidthManager service not found");
                }
            }
            else
            {
                Debug.Warn("[MoveToPitGenPoint] No queued pit level found, using current level + 1");
                
                // Fallback: increment current level
                var pitWidthManager = Core.Services.GetService<PitWidthManager>();
                if (pitWidthManager != null)
                {
                    var nextLevel = pitWidthManager.CurrentPitLevel + 1;
                    pitWidthManager.ReinitRightEdge();
                    pitWidthManager.SetPitLevel(nextLevel);
                    hero.PitInitialized = true;
                    
                    Debug.Log($"[MoveToPitGenPoint] Fallback: regenerated pit with level {nextLevel}");
                }
            }
        }

        /// <summary>
        /// Calculate AStar path from current position to target tile
        /// </summary>
        private List<Point> CalculatePathToTarget(HeroComponent hero, Point start, Point target)
        {
            try
            {
                // Use the hero's pathfinding component
                if (!hero.IsPathfindingInitialized)
                {
                    Debug.Warn("[MoveToPitGenPoint] Hero pathfinding not initialized");
                    return null;
                }

                Debug.Log($"[MoveToPitGenPoint] Calculating path from {start.X},{start.Y} to {target.X},{target.Y}");
                
                var path = hero.CalculatePath(start, target);
                
                if (path != null && path.Count > 0)
                {
                    Debug.Log($"[MoveToPitGenPoint] Found path with {path.Count} steps");
                }
                else
                {
                    Debug.Warn($"[MoveToPitGenPoint] No path found from {start.X},{start.Y} to {target.X},{target.Y}");
                }
                
                return path;
            }
            catch (System.Exception ex)
            {
                Debug.Error($"[MoveToPitGenPoint] Error calculating path: {ex.Message}");
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
            
            Debug.Warn($"[MoveToPitGenPoint] Non-adjacent tiles: from {from.X},{from.Y} to {to.X},{to.Y}");
            return null;
        }

        /// <summary>
        /// Reset internal execution state
        /// </summary>
        private void ResetInternal()
        {
            _currentPath = null;
            _pathIndex = 0;
        }
    }
}