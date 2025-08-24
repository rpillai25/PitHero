using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that moves the hero to the inside edge of the pit (2 tiles to the left of pit center)
    /// This prepares the hero for jumping out of the pit
    /// </summary>
    public class MovingToInsidePitEdgeAction : HeroActionBase
    {
        private List<Point> _currentPath;
        private int _pathIndex;
        private Point _targetTile;
        private bool _hasCalculatedTarget;
        
        public MovingToInsidePitEdgeAction() : base(GoapConstants.MovingToInsidePitEdgeAction, 1)
        {
            // Precondition: Hero should be moving to inside pit edge
            SetPrecondition(GoapConstants.MovingToInsidePitEdge, true);
            
            // Postconditions: Hero will be adjacent to pit boundary from inside and ready to jump out
            SetPostcondition(GoapConstants.AdjacentToPitBoundaryFromInside, true);
            SetPostcondition(GoapConstants.ReadyToJumpOutOfPit, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // Get the TileByTileMover component from the hero entity
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            
            if (tileMover == null)
            {
                Debug.Warn("MovingToInsidePitEdgeAction: Hero entity missing TileByTileMover component");
                ResetInternal();
                return true; // complete as failed
            }

            // Calculate target tile if we haven't yet
            if (!_hasCalculatedTarget)
            {
                _targetTile = CalculateInsidePitEdgeTarget();
                _hasCalculatedTarget = true;
                Debug.Log($"[MovingToInsidePitEdge] Target tile calculated: {_targetTile.X},{_targetTile.Y}");
            }

            // Check if we're already at the target position
            var currentTile = tileMover.GetCurrentTileCoordinates();
            if (currentTile.X == _targetTile.X && currentTile.Y == _targetTile.Y)
            {
                Debug.Log($"[MovingToInsidePitEdge] Reached target tile {_targetTile.X},{_targetTile.Y} - action complete");
                hero.AdjacentToPitBoundaryFromInside = true;
                hero.ReadyToJumpOutOfPit = true;
                hero.MovingToInsidePitEdge = false; // Clear the moving flag
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
                    Debug.Warn($"[MovingToInsidePitEdge] Could not find path from {currentTile.X},{currentTile.Y} to {_targetTile.X},{_targetTile.Y}");
                    ResetInternal();
                    return true; // Complete the action as failed
                }
                
                Debug.Log($"[MovingToInsidePitEdge] Calculated path with {_currentPath.Count} steps");
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
                            Debug.Log($"[MovingToInsidePitEdge] Moving to tile {nextTile.X},{nextTile.Y} (step {_pathIndex}/{_currentPath.Count})");
                        }
                        else
                        {
                            Debug.Log("[MovingToInsidePitEdge] Movement blocked, recalculating path");
                            _currentPath = null; // Force recalculation next frame
                        }
                    }
                    else
                    {
                        Debug.Warn($"[MovingToInsidePitEdge] Could not determine direction from {currentPos.X},{currentPos.Y} to {nextTile.X},{nextTile.Y}");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
                else
                {
                    // Reached end of path, check if we're at target
                    if (currentTile.X == _targetTile.X && currentTile.Y == _targetTile.Y)
                    {
                        Debug.Log($"[MovingToInsidePitEdge] Reached target tile {_targetTile.X},{_targetTile.Y} - action complete");
                        hero.AdjacentToPitBoundaryFromInside = true;
                        hero.ReadyToJumpOutOfPit = true;
                        hero.MovingToInsidePitEdge = false; // Clear the moving flag
                        ResetInternal();
                        return true;
                    }
                    else
                    {
                        Debug.Log("[MovingToInsidePitEdge] Path completed but not at target, recalculating");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
            }
            
            // Action continues as long as we're moving towards the target
            return false;
        }

        /// <summary>
        /// Calculate the inside pit edge target tile (2 tiles to the left of pit center)
        /// </summary>
        private Point CalculateInsidePitEdgeTarget()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            int pitCenterX, pitRightEdge;
            
            if (pitWidthManager != null)
            {
                pitCenterX = pitWidthManager.CurrentPitCenterTileX;
                pitRightEdge = pitWidthManager.CurrentPitRightEdge;
            }
            else
            {
                pitCenterX = GameConfig.PitCenterTileX;
                pitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth - 1;
            }
            
            // Target is 2 tiles to the left of the right edge (inside the pit boundary)
            var targetX = pitRightEdge - 2;
            var targetY = GameConfig.PitCenterTileY; // Use pit center Y coordinate
            
            Debug.Log($"[MovingToInsidePitEdge] Calculated target: pitRightEdge={pitRightEdge}, targetX={targetX}, targetY={targetY}");
            return new Point(targetX, targetY);
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
                    Debug.Warn("[MovingToInsidePitEdge] Hero pathfinding not initialized");
                    return null;
                }

                Debug.Log($"[MovingToInsidePitEdge] Calculating path from {start.X},{start.Y} to {target.X},{target.Y}");
                
                var path = hero.CalculatePath(start, target);
                
                if (path != null && path.Count > 0)
                {
                    Debug.Log($"[MovingToInsidePitEdge] Found path with {path.Count} steps");
                }
                else
                {
                    Debug.Warn($"[MovingToInsidePitEdge] No path found from {start.X},{start.Y} to {target.X},{target.Y}");
                }
                
                return path;
            }
            catch (System.Exception ex)
            {
                Debug.Error($"[MovingToInsidePitEdge] Error calculating path: {ex.Message}");
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
            
            Debug.Warn($"[MovingToInsidePitEdge] Non-adjacent tiles: from {from.X},{from.Y} to {to.X},{to.Y}");
            return null;
        }

        /// <summary>
        /// Reset internal execution state
        /// </summary>
        private void ResetInternal()
        {
            _currentPath = null;
            _pathIndex = 0;
            _hasCalculatedTarget = false;
        }
    }
}