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
        private HeroComponent _heroComponent;
        private AI.MercenaryStateMachine _stateMachine;
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

            // Hard gate: following is only allowed while this merc and the hero are on the same
            // side of the pit boundary. Crossing the boundary is exclusively the GOAP jump
            // actions' job — this component must never drag a merc into or out of the pit.
            var hero = GetHeroComponent();
            if (hero != null && hero.InsidePit != _mercComponent.InsidePit)
            {
                _currentPath = null;
                _stuckTimer = 0f;
                return;
            }

            // Hard gate: only the FollowTargetAction may drive this component. While the state
            // machine runs any other action (walk-to-edge, a jump) or waits with no plan, moving
            // toward the follow target would fight that action for the mover/transform — the
            // "jump and bounce back off the pit wall" loop.
            var stateMachine = GetStateMachine();
            if (stateMachine != null && !stateMachine.IsExecutingFollowAction)
            {
                _currentPath = null;
                _stuckTimer = 0f;
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
                // Never warp onto an impassable tile — the target's truncated position sits on the
                // pit wall while it lerps through a jump. Keep the timer running and retry next frame.
                if (_pathfinding.PathfindingGraph?.Walls?.Contains(targetTile) == true)
                {
                    return;
                }

                // Never warp across the pit boundary — pit crossing is exclusively the GOAP jump
                // actions' job. Keep the timer running; the state machine will replan a jump.
                var pitWidthManager = Core.Services.GetService<PitWidthManager>();
                if (pitWidthManager != null && pitWidthManager.IsTileInsidePitInterior(targetTile) != _mercComponent.InsidePit)
                {
                    return;
                }

                var warpTile = PickWarpTile(targetTile, myTile);
                Debug.Warn($"[MercenaryFollowComponent] {Entity.Name} stuck at ({myTile.X},{myTile.Y}) for {_stuckTimer:F1}s, warping to ({warpTile.X},{warpTile.Y}) near target ({targetTile.X},{targetTile.Y})");
                _tileMover.WarpToTile(warpTile);
                _pathfinding.RefreshPathfindingWithObstacles();

                _currentPath = null;
                _pathIndex = 0;
                _stuckTimer = 0f;
                _lastStuckCheckTile = warpTile;
                _mercComponent.LastTilePosition = warpTile;
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
        /// Resolves this merc's state machine (lazy — the follow component can be added before
        /// or after the state machine depending on the hire flow)
        /// </summary>
        private AI.MercenaryStateMachine GetStateMachine()
        {
            if (_stateMachine == null)
                _stateMachine = Entity.GetComponent<AI.MercenaryStateMachine>();
            return _stateMachine;
        }

        /// <summary>
        /// Picks a free tile orthogonally adjacent to the follow target for a stuck-warp so the
        /// merc never lands on top of the target or another party member. Falls back to the
        /// target tile itself if every neighbor is walled, across the pit boundary, or occupied.
        /// </summary>
        private Point PickWarpTile(Point targetTile, Point myTile)
        {
            var candidates = new Point[]
            {
                new Point(targetTile.X - 1, targetTile.Y),
                new Point(targetTile.X + 1, targetTile.Y),
                new Point(targetTile.X, targetTile.Y - 1),
                new Point(targetTile.X, targetTile.Y + 1),
            };

            // Nearest to the merc first so the warp covers the least distance
            System.Array.Sort(candidates, (a, b) =>
                (System.Math.Abs(a.X - myTile.X) + System.Math.Abs(a.Y - myTile.Y))
                    .CompareTo(System.Math.Abs(b.X - myTile.X) + System.Math.Abs(b.Y - myTile.Y)));

            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (_pathfinding.PathfindingGraph?.Walls?.Contains(candidate) == true)
                    continue;
                if (pitWidthManager != null && pitWidthManager.IsTileInsidePitInterior(candidate) != _mercComponent.InsidePit)
                    continue;
                if (IsTileOccupiedByPartyMember(candidate))
                    continue;
                return candidate;
            }

            return targetTile;
        }

        /// <summary>True when the hero or another hired mercenary currently stands on the tile.</summary>
        private bool IsTileOccupiedByPartyMember(Point tile)
        {
            var hero = GetHeroComponent();
            if (hero?.Entity != null && GetEntityTile(hero.Entity) == tile)
                return true;

            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                var hired = mercenaryManager.GetHiredMercenaries();
                for (int i = 0; i < hired.Count; i++)
                {
                    if (hired[i] != Entity && GetEntityTile(hired[i]) == tile)
                        return true;
                }
            }

            return false;
        }

        private static Point GetEntityTile(Entity entity)
        {
            var pos = entity.Transform.Position;
            return new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );
        }

        /// <summary>
        /// Resolves the hero component, re-resolving when stale (hero promotion destroys the
        /// old hero entity and spawns a new one)
        /// </summary>
        private HeroComponent GetHeroComponent()
        {
            if (_heroComponent == null || _heroComponent.Entity == null || _heroComponent.Entity.IsDestroyed)
                _heroComponent = Entity?.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            return _heroComponent;
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

