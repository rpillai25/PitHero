using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.Pathfinding;
using PitHero.ECS.Components;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that uses AStar pathfinding to move the hero to the edge of the pit
    /// </summary>
    public class MoveToPitAction : HeroActionBase
    {
        // Fallback candidate targets - used if PitWidthManager is not available
        private static readonly Point[] s_fallbackCandidateTargets = new Point[]
        {
            new Point(13, 3),
            new Point(13, 4),
            new Point(13, 5),
            new Point(13, 6),
            new Point(13, 7),
            new Point(13, 8),
            new Point(13, 9)
        };

        private List<Point> _currentPath;
        private int _pathIndex;
        private Point _targetTile; // Chosen at runtime from candidates
        private bool _hasSelectedTarget;

        public MoveToPitAction() : base(GoapConstants.MoveToPitAction, 1)
        {
            // Preconditions: Hero and pit must be initialized
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            
            // Postcondition: Hero will be adjacent to pit boundary from outside
            SetPostcondition(GoapConstants.AdjacentToPitBoundaryFromOutside, true);
        }

        /// <summary>
        /// Execute the action each frame; moves along path toward chosen pit-adjacent target
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // Get the TileByTileMover component from the hero entity
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            
            if (tileMover == null)
            {
                Debug.Warn("MoveToPitAction: Hero entity missing TileByTileMover component");
                ResetInternal();
                return true; // complete as failed
            }

            // Choose a target if we have not yet selected one for this execution
            if (!_hasSelectedTarget)
            {
                // Get current candidate targets from PitWidthManager
                var candidateTargets = GetCurrentCandidateTargets();
                var idx = Nez.Random.NextInt(candidateTargets.Length);
                _targetTile = candidateTargets[idx];
                _hasSelectedTarget = true;
                Debug.Log($"[MoveToPit] Selected target tile {_targetTile.X},{_targetTile.Y}");
            }

            // Check if we're already at the target position
            var currentTile = tileMover.GetCurrentTileCoordinates();
            if (currentTile.X == _targetTile.X && currentTile.Y == _targetTile.Y)
            {
                Debug.Log($"[MoveToPit] Reached target tile {_targetTile.X},{_targetTile.Y} - action complete");
                hero.AdjacentToPitBoundaryFromOutside = true;
                hero.PitApproachDirection = Direction.Left; // Approaching from right side of pit
                ResetInternal();
                return true;
            }

            // If we don't have a path yet, calculate one
            if (_currentPath == null || _currentPath.Count == 0)
            {
                _currentPath = CalculatePathToTarget(hero, currentTile, _targetTile);
                _pathIndex = 0;
                
                if (_currentPath == null || _currentPath.Count == 0)
                {
                    Debug.Warn($"[MoveToPit] Could not find path from {currentTile.X},{currentTile.Y} to {_targetTile.X},{_targetTile.Y}");
                    ResetInternal();
                    return true; // Complete the action as failed
                }
                
                Debug.Log($"[MoveToPit] Calculated path with {_currentPath.Count} steps");
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
                            Debug.Log($"[MoveToPit] Moving to tile {nextTile.X},{nextTile.Y} (step {_pathIndex}/{_currentPath.Count})");
                        }
                        else
                        {
                            Debug.Log("[MoveToPit] Movement blocked, recalculating path");
                            _currentPath = null; // Force recalculation next frame
                        }
                    }
                    else
                    {
                        Debug.Warn($"[MoveToPit] Could not determine direction from {currentPos.X},{currentPos.Y} to {nextTile.X},{nextTile.Y}");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
                else
                {
                    // Reached end of path, check if we're at target
                    if (currentTile.X == _targetTile.X && currentTile.Y == _targetTile.Y)
                    {
                        Debug.Log($"[MoveToPit] Reached target tile {_targetTile.X},{_targetTile.Y} - action complete");
                        hero.AdjacentToPitBoundaryFromOutside = true;
                        hero.PitApproachDirection = Direction.Left; // Approaching from right side of pit
                        ResetInternal();
                        return true;
                    }
                    else
                    {
                        Debug.Log("[MoveToPit] Path completed but not at target, recalculating");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
            }
            
            // Action continues as long as we're moving towards the pit
            return false;
        }

        /// <summary>
        /// Calculate AStar path from current position to target tile
        /// </summary>
        private List<Point> CalculatePathToTarget(HeroComponent hero, Point start, Point target)
        {
            try
            {
                // Get the AStarGridGraph from the scene service
                var astarGraph = Core.Services.GetService<AstarGridGraph>();
                
                if (astarGraph == null)
                {
                    Debug.Warn("[MoveToPit] AStarGridGraph service not found");
                    return null;
                }

                Debug.Log($"[MoveToPit] Calculating path from {start.X},{start.Y} to {target.X},{target.Y}");
                
                var path = astarGraph.Search(start, target);
                
                if (path != null && path.Count > 0)
                {
                    Debug.Log($"[MoveToPit] Found path with {path.Count} steps");
                    // Remove the first point if it's the current position
                    if (path.Count > 0 && path[0].X == start.X && path[0].Y == start.Y)
                    {
                        path.RemoveAt(0);
                    }
                }
                else
                {
                    Debug.Warn($"[MoveToPit] No path found from {start.X},{start.Y} to {target.X},{target.Y}");
                }
                
                return path;
            }
            catch (System.Exception ex)
            {
                Debug.Error($"[MoveToPit] Error calculating path: {ex.Message}");
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
            
            Debug.Warn($"[MoveToPit] Non-adjacent tiles: from {from.X},{from.Y} to {to.X},{to.Y}");
            return null;
        }

        /// <summary>
        /// Get current candidate targets from PitWidthManager, or fallback if not available
        /// </summary>
        private Point[] GetCurrentCandidateTargets()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null)
            {
                return pitWidthManager.GetCurrentPitCandidateTargets();
            }
            else
            {
                Debug.Warn("[MoveToPit] PitWidthManager not available, using fallback targets");
                return s_fallbackCandidateTargets;
            }
        }

        /// <summary>
        /// Reset internal execution state so future runs can reselect target
        /// </summary>
        private void ResetInternal()
        {
            _currentPath = null;
            _pathIndex = 0;
            _hasSelectedTarget = false;
        }

        /// <summary>
        /// Public method to reset the action's internal state for target recalculation
        /// </summary>
        public void ResetTargetSelection()
        {
            ResetInternal();
        }
    }
}