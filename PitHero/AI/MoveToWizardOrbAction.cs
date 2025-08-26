using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.AI.Interfaces;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to move to the wizard orb after the map is fully explored
    /// Uses AStar pathfinding to navigate to the wizard orb location
    /// </summary>
    public class MoveToWizardOrbAction : HeroActionBase
    {
        private List<Point> _currentPath;
        private int _pathIndex;
        private Point _wizardOrbTile;
        private bool _hasFoundWizardOrb;
        
        public MoveToWizardOrbAction() : base(GoapConstants.MoveToWizardOrbAction, 1)
        {
            // Preconditions: Map must be explored and wizard orb must be found
            SetPrecondition(GoapConstants.FoundWizardOrb, true);
            SetPrecondition(GoapConstants.MapExplored, true);

            // Postcondition: Hero will be at wizard orb
            SetPostcondition(GoapConstants.AtWizardOrb, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // Get the TileByTileMover component from the hero entity
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            
            if (tileMover == null)
            {
                Debug.Warn("MoveToWizardOrbAction: Hero entity missing TileByTileMover component");
                ResetInternal();
                return true; // complete as failed
            }

            // Find wizard orb if we haven't yet
            if (!_hasFoundWizardOrb)
            {
                _wizardOrbTile = FindWizardOrbTile();
                if (_wizardOrbTile.X == -1)
                {
                    Debug.Warn("[MoveToWizardOrb] Could not find wizard orb entity");
                    ResetInternal();
                    return true; // complete as failed
                }
                _hasFoundWizardOrb = true;
                Debug.Log($"[MoveToWizardOrb] Found wizard orb at tile {_wizardOrbTile.X},{_wizardOrbTile.Y}");
            }

            // Check if we're already at the wizard orb position
            var currentTile = tileMover.GetCurrentTileCoordinates();
            if (currentTile.X == _wizardOrbTile.X && currentTile.Y == _wizardOrbTile.Y)
            {
                Debug.Log($"[MoveToWizardOrb] Reached wizard orb at tile {_wizardOrbTile.X},{_wizardOrbTile.Y} - action complete");
                ResetInternal();
                return true;
            }

            // If we don't have a path yet, calculate one
            if (_currentPath == null || _currentPath.Count == 0)
            {
                _currentPath = CalculatePathToTarget(hero, currentTile, _wizardOrbTile);
                _pathIndex = 0;
                
                if (_currentPath == null || _currentPath.Count == 0)
                {
                    Debug.Warn($"[MoveToWizardOrb] Could not find path from {currentTile.X},{currentTile.Y} to {_wizardOrbTile.X},{_wizardOrbTile.Y}");
                    ResetInternal();
                    return true; // Complete the action as failed
                }
                
                Debug.Log($"[MoveToWizardOrb] Calculated path with {_currentPath.Count} steps");
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
                            Debug.Log($"[MoveToWizardOrb] Moving to tile {nextTile.X},{nextTile.Y} (step {_pathIndex}/{_currentPath.Count})");
                        }
                        else
                        {
                            Debug.Log("[MoveToWizardOrb] Movement blocked, recalculating path");
                            _currentPath = null; // Force recalculation next frame
                        }
                    }
                    else
                    {
                        Debug.Warn($"[MoveToWizardOrb] Could not determine direction from {currentPos.X},{currentPos.Y} to {nextTile.X},{nextTile.Y}");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
                else
                {
                    // Reached end of path, check if we're at target
                    if (currentTile.X == _wizardOrbTile.X && currentTile.Y == _wizardOrbTile.Y)
                    {
                        Debug.Log($"[MoveToWizardOrb] Reached wizard orb at tile {_wizardOrbTile.X},{_wizardOrbTile.Y} - action complete");
                        ResetInternal();
                        return true;
                    }
                    else
                    {
                        Debug.Log("[MoveToWizardOrb] Path completed but not at target, recalculating");
                        _currentPath = null; // Force recalculation next frame
                    }
                }
            }
            
            // Action continues as long as we're moving towards the wizard orb
            return false;
        }

        /// <summary>
        /// Execute action using interface-based context (new approach)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[MoveToWizardOrbAction] Starting execution with interface-based context");

            // Find wizard orb if we haven't yet
            if (!_hasFoundWizardOrb)
            {
                var orbPos = context.WorldState.WizardOrbPosition;
                if (!orbPos.HasValue)
                {
                    context.LogWarning("[MoveToWizardOrbAction] No wizard orb position available");
                    ResetInternal();
                    return true; // complete as failed
                }
                
                _wizardOrbTile = orbPos.Value;
                _hasFoundWizardOrb = true;
                context.LogDebug($"[MoveToWizardOrbAction] Found wizard orb at tile {_wizardOrbTile.X},{_wizardOrbTile.Y}");
            }

            // Check if we're already at the wizard orb position
            var currentTile = context.HeroController.CurrentTilePosition;
            if (currentTile.X == _wizardOrbTile.X && currentTile.Y == _wizardOrbTile.Y)
            {
                context.LogDebug($"[MoveToWizardOrbAction] Reached wizard orb at tile {_wizardOrbTile.X},{_wizardOrbTile.Y} - action complete");
                ResetInternal();
                return true;
            }

            // If we don't have a path yet, calculate one
            if (_currentPath == null || _currentPath.Count == 0)
            {
                _currentPath = context.Pathfinder.CalculatePath(currentTile, _wizardOrbTile);
                _pathIndex = 0;
                
                if (_currentPath == null || _currentPath.Count == 0)
                {
                    context.LogWarning($"[MoveToWizardOrbAction] Could not find path from {currentTile.X},{currentTile.Y} to {_wizardOrbTile.X},{_wizardOrbTile.Y}");
                    ResetInternal();
                    return true; // Complete the action as failed
                }
                
                context.LogDebug($"[MoveToWizardOrbAction] Calculated path with {_currentPath.Count} steps");
            }

            // If not currently moving, start moving to the next tile in the path
            if (!context.HeroController.IsMoving && _pathIndex < _currentPath.Count)
            {
                var nextTile = _currentPath[_pathIndex];
                var direction = GetDirectionToTile(currentTile, nextTile);

                if (direction.HasValue)
                {
                    context.LogDebug($"[MoveToWizardOrbAction] Moving {direction.Value} to tile {nextTile.X},{nextTile.Y}");
                    var moveStarted = context.HeroController.StartMoving(direction.Value);
                    if (moveStarted)
                    {
                        _pathIndex++;
                    }
                    else
                    {
                        context.LogWarning("[MoveToWizardOrbAction] Movement blocked, recalculating path");
                        _currentPath = null; // Force path recalculation
                    }
                }
                else
                {
                    context.LogWarning($"[MoveToWizardOrbAction] Invalid movement from {currentTile.X},{currentTile.Y} to {nextTile.X},{nextTile.Y}");
                    _currentPath = null; // Force path recalculation
                    return false; // try again next tick
                }
            }

            return false; // Action still in progress
        }

        /// <summary>
        /// Find the wizard orb tile position in the scene
        /// </summary>
        private Point FindWizardOrbTile()
        {
            var scene = Core.Scene;
            if (scene == null)
            {
                Debug.Warn("[MoveToWizardOrb] No active scene found");
                return new Point(-1, -1);
            }

            var wizardOrbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            if (wizardOrbEntities.Count == 0)
            {
                Debug.Warn("[MoveToWizardOrb] No wizard orb entities found");
                return new Point(-1, -1);
            }

            var wizardOrbEntity = wizardOrbEntities[0]; // Should only be one wizard orb
            var worldPos = wizardOrbEntity.Transform.Position;
            var tilePos = new Point((int)(worldPos.X / GameConfig.TileSize), (int)(worldPos.Y / GameConfig.TileSize));
            
            Debug.Log($"[MoveToWizardOrb] Found wizard orb at world pos {worldPos.X},{worldPos.Y}, tile {tilePos.X},{tilePos.Y}");
            return tilePos;
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
                    Debug.Warn("[MoveToWizardOrb] Hero pathfinding not initialized");
                    return null;
                }

                Debug.Log($"[MoveToWizardOrb] Calculating path from {start.X},{start.Y} to {target.X},{target.Y}");
                
                var path = hero.CalculatePath(start, target);
                
                if (path != null && path.Count > 0)
                {
                    Debug.Log($"[MoveToWizardOrb] Found path with {path.Count} steps");
                }
                else
                {
                    Debug.Warn($"[MoveToWizardOrb] No path found from {start.X},{start.Y} to {target.X},{target.Y}");
                }
                
                return path;
            }
            catch (System.Exception ex)
            {
                Debug.Error($"[MoveToWizardOrb] Error calculating path: {ex.Message}");
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
            
            Debug.Warn($"[MoveToWizardOrb] Non-adjacent tiles: from {from.X},{from.Y} to {to.X},{to.Y}");
            return null;
        }

        /// <summary>
        /// Reset internal execution state
        /// </summary>
        private void ResetInternal()
        {
            _currentPath = null;
            _pathIndex = 0;
            _hasFoundWizardOrb = false;
        }
    }
}