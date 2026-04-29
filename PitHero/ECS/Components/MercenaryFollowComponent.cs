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

        // Stuck detection
        private float _stuckTimer;
        private Point _lastStuckCheckTile;

        public override void OnAddedToEntity()
        {
            _tileMover = Entity.GetComponent<TileByTileMover>();
            _mercComponent = Entity.GetComponent<MercenaryComponent>();
            _pathfinding = Entity.GetComponent<PathfindingActorComponent>();
            _currentPath = null;
            _pathIndex = 0;
            _lastTargetTile = new Point(-1, -1);
            _stuckTimer = 0f;
            _lastStuckCheckTile = new Point(-1, -1);
        }

        public void Update()
        {
            // Debug.Log($"[MercenaryFollowComponent] Update() called for {Entity.Name}");

            // Early return if component is disabled (e.g., during sleep)
            if (!Enabled)
            {
                return;
            }

            if (_mercComponent == null || !_mercComponent.IsHired)
            {
                return;
            }

            if (_mercComponent.FollowTarget == null)
            {
                return;
            }

            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
            {
                // Debug.Log($"[MercenaryFollowComponent] {Entity.Name} game is paused");
                return;
            }

            if (_pathfinding == null || !_pathfinding.IsPathfindingInitialized)
            {
                return;
            }

            if (_tileMover != null && _tileMover.IsMoving)
            {
                // Debug.Log($"[MercenaryFollowComponent] {Entity.Name} already moving");
                _stuckTimer = 0f; // Reset stuck timer while actively moving
                return;
            }

            var myTile = GetCurrentTile();
            // Debug.Log($"[MercenaryFollowComponent] {Entity.Name} at tile ({myTile.X},{myTile.Y})");

            // Track stuck detection: accumulate time at the same tile
            if (myTile == _lastStuckCheckTile)
            {
                _stuckTimer += Time.DeltaTime;
            }
            else
            {
                _stuckTimer = 0f;
                _lastStuckCheckTile = myTile;
            }

            if (_tileMover != null && _tileMover.Enabled && _mercComponent.LastTilePosition != myTile)
            {
                _mercComponent.LastTilePosition = myTile;
            }

            var targetTile = GetTargetCurrentTilePosition();
            // Debug.Log($"[MercenaryFollowComponent] {Entity.Name} target current tile ({targetTile.X},{targetTile.Y})");

            // Check if we're already at the target position
            if (myTile == targetTile)
            {
                // Debug.Log($"[MercenaryFollowComponent] {Entity.Name} already at target position");
                _currentPath = null;
                return;
            }

            // Check if adjacent to target to avoid occupying the same tile
            var dx = System.Math.Abs(myTile.X - targetTile.X);
            var dy = System.Math.Abs(myTile.Y - targetTile.Y);
            bool isAdjacent = (dx == 1 && dy == 0) || (dx == 0 && dy == 1) || (dx == 1 && dy == 1);

            if (isAdjacent)
            {
                // Debug.Log($"[MercenaryFollowComponent] {Entity.Name} adjacent to target, stopping to avoid overlap");
                _currentPath = null;
                _stuckTimer = 0f;
                return;
            }

            // Stuck detection: if mercenary has been at the same tile too long while needing to move, warp near target
            if (_stuckTimer >= GameConfig.MovementStuckTimeoutSeconds && _tileMover != null)
            {
                Debug.Warn($"[MercenaryFollowComponent] {Entity.Name} stuck at ({myTile.X},{myTile.Y}) for {_stuckTimer:F1}s, warping near target ({targetTile.X},{targetTile.Y})");
                _tileMover.WarpToTile(targetTile);
                _currentPath = null;
                _pathIndex = 0;
                _stuckTimer = 0f;
                _lastStuckCheckTile = targetTile;
                _mercComponent.LastTilePosition = targetTile;
                return;
            }

            // Recalculate path if target moved OR if we don't have a valid path
            bool needsNewPath = targetTile != _lastTargetTile || _currentPath == null || _currentPath.Count == 0;
            
            if (needsNewPath)
            {
                // Ensure pathfinding graph is up to date (safety check)
                if (_pathfinding.PathfindingGraph?.Walls == null)
                {
                    Debug.Warn($"[MercenaryFollowComponent] {Entity.Name} pathfinding graph not properly initialized - refreshing");
                    _pathfinding.RefreshPathfinding();
                }
                
                _lastTargetTile = targetTile;
                _currentPath = _pathfinding.CalculateFogAwarePath(myTile, targetTile);
                _pathIndex = 0;

                if (_currentPath == null || _currentPath.Count == 0)
                {
                    return;
                }
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
            _stuckTimer = 0f;
            _lastStuckCheckTile = new Point(-1, -1);
        }
    }
}

