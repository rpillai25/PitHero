using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that makes hired mercenaries follow their target (hero or another mercenary) using A* pathfinding.
    /// This component has a single responsibility: pathfind to the target's last known position.
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
            Debug.Log($"[MercenaryFollowComponent] Update() called for {Entity.Name}");

            if (_mercComponent == null || !_mercComponent.IsHired)
            {
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} early return: merc={_mercComponent != null}, hired={_mercComponent?.IsHired}");
                return;
            }

            if (_mercComponent.FollowTarget == null)
            {
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} no follow target");
                return;
            }

            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
            {
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} game is paused");
                return;
            }

            if (_pathfinding == null || !_pathfinding.IsPathfindingInitialized)
            {
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} pathfinding not initialized");
                return;
            }

            if (_tileMover != null && _tileMover.IsMoving)
            {
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} already moving");
                return;
            }

            var myTile = GetCurrentTile();
            Debug.Log($"[MercenaryFollowComponent] {Entity.Name} at tile ({myTile.X},{myTile.Y})");

            if (_tileMover != null && _tileMover.Enabled && _mercComponent.LastTilePosition != myTile)
            {
                _mercComponent.LastTilePosition = myTile;
            }

            var targetLastTile = GetTargetLastTilePosition();
            Debug.Log($"[MercenaryFollowComponent] {Entity.Name} target last tile ({targetLastTile.X},{targetLastTile.Y})");

            // Check if we're already at the target position
            if (myTile == targetLastTile)
            {
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} already at target position");
                _currentPath = null;
                return;
            }

            // Check if the target is currently on their last tile position
            // If so, stop one tile away to avoid occupying the same tile
            var targetCurrentTile = GetTargetCurrentTilePosition();
            if (targetCurrentTile == targetLastTile)
            {
                // Target is still on their last position, check if we're adjacent
                var dx = System.Math.Abs(myTile.X - targetCurrentTile.X);
                var dy = System.Math.Abs(myTile.Y - targetCurrentTile.Y);
                bool isAdjacent = (dx == 1 && dy == 0) || (dx == 0 && dy == 1);

                if (isAdjacent)
                {
                    Debug.Log($"[MercenaryFollowComponent] {Entity.Name} adjacent to target, stopping to avoid overlap");
                    _currentPath = null;
                    return;
                }
            }

            if (targetLastTile != _lastTargetTile)
            {
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} target moved, recalculating path");
                _lastTargetTile = targetLastTile;
                _currentPath = _pathfinding.CalculatePath(myTile, targetLastTile);
                _pathIndex = 0;

                if (_currentPath == null || _currentPath.Count == 0)
                {
                    Debug.Log($"[MercenaryFollowComponent] {Entity.Name} no path found to target");
                    return;
                }
                Debug.Log($"[MercenaryFollowComponent] {Entity.Name} found path with {_currentPath.Count} steps");
            }

            if (_currentPath != null && _pathIndex < _currentPath.Count)
            {
                var nextTile = _currentPath[_pathIndex];

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

                var direction = GetDirectionToTile(myTile, nextTile);
                if (direction.HasValue && _tileMover != null)
                {
                    _tileMover.StartMoving(direction.Value);
                }
            }
        }

        /// <summary>
        /// Gets the current tile position of this mercenary
        /// </summary>
        private Point GetCurrentTile()
        {
            var pos = Entity.Transform.Position;
            return new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );
        }

        /// <summary>
        /// Gets the last tile position of the target (hero or another mercenary)
        /// </summary>
        private Point GetTargetLastTilePosition()
        {
            var targetHeroComponent = _mercComponent.FollowTarget.GetComponent<HeroComponent>();
            var targetMercComponent = _mercComponent.FollowTarget.GetComponent<MercenaryComponent>();
            
            if (targetHeroComponent != null)
            {
                return targetHeroComponent.LastTilePosition;
            }
            else if (targetMercComponent != null)
            {
                return targetMercComponent.LastTilePosition;
            }

            return GetCurrentTile();
        }

        /// <summary>
        /// Gets the current real-time tile position of the target entity
        /// </summary>
        private Point GetTargetCurrentTilePosition()
        {
            if (_mercComponent.FollowTarget == null)
            {
                return new Point(-1, -1);
            }

            var pos = _mercComponent.FollowTarget.Transform.Position;
            return new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );
        }

        /// <summary>
        /// Gets the direction to move from current tile to target tile (adjacent only)
        /// </summary>
        private Direction? GetDirectionToTile(Point current, Point target)
        {
            var dx = target.X - current.X;
            var dy = target.Y - current.Y;

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

