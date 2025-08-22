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
        private List<Point> _currentPath;
        private int _pathIndex;
        private Point _targetTile = new Point(13, 6); // Target tile just outside pit entrance

        public MoveToPitAction() : base(GoapConstants.MoveToPitAction, 1)
        {
            // Preconditions: Hero and pit must be initialized
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            
            // Postcondition: Hero will be adjacent to pit boundary from outside
            SetPostcondition(GoapConstants.AdjacentToPitBoundaryFromOutside, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // Get the TileByTileMover component from the hero entity
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            
            if (tileMover == null)
            {
                Debug.Warn("MoveToPitAction: Hero entity missing TileByTileMover component");
                return false;
            }

            // Check if we're already at the target position
            var currentTile = tileMover.GetCurrentTileCoordinates();
            if (currentTile.X == _targetTile.X && currentTile.Y == _targetTile.Y)
            {
                Debug.Log($"[MoveToPit] Reached target tile {_targetTile.X},{_targetTile.Y} - action complete");
                hero.AdjacentToPitBoundaryFromOutside = true;
                hero.PitApproachDirection = Direction.Left; // Approaching from right side of pit
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
                            Debug.Log($"[MoveToPit] Movement blocked, recalculating path");
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
                        return true;
                    }
                    else
                    {
                        Debug.Log($"[MoveToPit] Path completed but not at target, recalculating");
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
    }
}