using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that makes hired mercenaries follow their target (hero or another mercenary) using A* pathfinding
    /// </summary>
    public class MercenaryFollowComponent : Component, IUpdatable
    {
        private TileByTileMover _tileMover;
        private MercenaryComponent _mercComponent;
        private PathfindingActorComponent _pathfinding;
        private List<Point> _currentPath;
        private int _pathIndex;
        private Point _lastTargetTile;

        public override void OnAddedToEntity()
        {
            _tileMover = Entity.GetComponent<TileByTileMover>();
            _mercComponent = Entity.GetComponent<MercenaryComponent>();
            _pathfinding = Entity.GetComponent<PathfindingActorComponent>();
            _currentPath = null;
            _pathIndex = 0;
            _lastTargetTile = new Point(-1, -1);
        }

        public void Update()
        {
            if (_mercComponent == null || !_mercComponent.IsHired)
                return;

            if (_mercComponent.FollowTarget == null)
                return;

            // Check if game is paused
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            // Wait for pathfinding to initialize
            if (_pathfinding == null || !_pathfinding.IsPathfindingInitialized)
                return;

            // Don't move if already moving
            if (_tileMover != null && _tileMover.IsMoving)
                return;

            // Get our current tile position
            var myPos = Entity.Transform.Position;
            var myTile = new Point(
                (int)(myPos.X / GameConfig.TileSize),
                (int)(myPos.Y / GameConfig.TileSize)
            );

            // Update our last tile position when we move (but only if we're not disabled/frozen)
            // This prevents overwriting LastTilePosition during teleportation/repositioning
            if (_tileMover != null && _tileMover.Enabled && _mercComponent.LastTilePosition != myTile)
            {
                Debug.Log($"[MercenaryFollow] {Entity.Name} updating LastTilePosition from ({_mercComponent.LastTilePosition.X},{_mercComponent.LastTilePosition.Y}) to ({myTile.X},{myTile.Y})");
                _mercComponent.LastTilePosition = myTile;
            }

            // Get target's last tile position (where we should move to)
            Point targetLastTile;
            var targetHeroComponent = _mercComponent.FollowTarget.GetComponent<HeroComponent>();
            var targetMercComponent = _mercComponent.FollowTarget.GetComponent<MercenaryComponent>();
            
            if (targetHeroComponent != null)
            {
                // Following hero - use their LastTilePosition
                targetLastTile = targetHeroComponent.LastTilePosition;
                Debug.Log($"[MercenaryFollow] {Entity.Name} reading hero's LastTilePosition: ({targetLastTile.X},{targetLastTile.Y}), hero world pos: ({targetHeroComponent.Entity.Transform.Position.X},{targetHeroComponent.Entity.Transform.Position.Y})");
            }
            else if (targetMercComponent != null)
            {
                // Following another mercenary - use their LastTilePosition
                targetLastTile = targetMercComponent.LastTilePosition;
                Debug.Log($"[MercenaryFollow] {Entity.Name} reading target mercenary's LastTilePosition: ({targetLastTile.X},{targetLastTile.Y})");
            }
            else
            {
                // No valid target component
                return;
            }

            // If we're already at the target position, we're done
            if (myTile == targetLastTile)
            {
                _currentPath = null;
                Debug.Log($"[MercenaryFollow] {Entity.Name} already at target position ({myTile.X},{myTile.Y}), no movement needed");
                return;
            }

            // If target moved to a new position, recalculate path
            if (targetLastTile != _lastTargetTile)
            {
                Debug.Log($"[MercenaryFollow] {Entity.Name} target moved from ({_lastTargetTile.X},{_lastTargetTile.Y}) to ({targetLastTile.X},{targetLastTile.Y}), recalculating path");
                _lastTargetTile = targetLastTile;
                _currentPath = _pathfinding.CalculatePath(myTile, targetLastTile);
                _pathIndex = 0;

                if (_currentPath == null || _currentPath.Count == 0)
                {
                    // No path found - wait for next update
                    return;
                }
            }

            // Follow the current path
            if (_currentPath != null && _pathIndex < _currentPath.Count)
            {
                var nextTile = _currentPath[_pathIndex];

                // If we're at the next tile in path, advance to next
                if (myTile == nextTile)
                {
                    _pathIndex++;
                    if (_pathIndex >= _currentPath.Count)
                    {
                        _currentPath = null;
                        return;
                    }
                    nextTile = _currentPath[_pathIndex];
                }

                // Move toward next tile in path
                var direction = GetDirectionToTile(myTile, nextTile);
                if (direction.HasValue && _tileMover != null)
                {
                    _tileMover.StartMoving(direction.Value);
                }
            }
        }

        /// <summary>
        /// Gets the direction to move from current tile to target tile (adjacent only)
        /// </summary>
        private Direction? GetDirectionToTile(Point current, Point target)
        {
            var dx = target.X - current.X;
            var dy = target.Y - current.Y;

            // Only handle adjacent tiles
            if (dx > 0 && dy == 0) return Direction.Right;
            if (dx < 0 && dy == 0) return Direction.Left;
            if (dy > 0 && dx == 0) return Direction.Down;
            if (dy < 0 && dx == 0) return Direction.Up;

            return null;
        }

        /// <summary>
        /// Reset pathfinding state (used when mercenary is teleported)
        /// </summary>
        public void ResetPathfinding()
        {
            _currentPath = null;
            _pathIndex = 0;
            _lastTargetTile = new Point(-1, -1);
            Debug.Log($"[MercenaryFollow] Pathfinding state reset for {Entity.Name}");
        }
    }
}
