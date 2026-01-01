using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that handles a newly hired mercenary joining their follow target
    /// Handles different scenarios: outside pit, inside pit, jumping into pit
    /// </summary>
    public class MercenaryJoinComponent : Component, IUpdatable
    {
        private enum JoinState
        {
            Idle,
            PathfindingToTarget,
            WalkingToPitEdge,
            JumpingIntoPit,
            Complete
        }

        private TileByTileMover _tileMover;
        private MercenaryComponent _mercComponent;
        private PathfindingActorComponent _pathfinding;
        private HeroJumpComponent _jumpComponent;
        private JoinState _state = JoinState.Idle;
        private List<Point> _currentPath;
        private int _currentPathIndex;
        private Point _lastTargetTile = new Point(-1, -1);
        private Point _pitEdgeTile;
        private bool _isPerformingAction; // Flag to prevent state changes during coroutines

        public override void OnAddedToEntity()
        {
            _tileMover = Entity.GetComponent<TileByTileMover>();
            _mercComponent = Entity.GetComponent<MercenaryComponent>();
            _pathfinding = Entity.GetComponent<PathfindingActorComponent>();
            _jumpComponent = Entity.GetComponent<HeroJumpComponent>();

            if (_jumpComponent == null)
            {
                _jumpComponent = Entity.AddComponent(new HeroJumpComponent());
            }

            _state = JoinState.PathfindingToTarget;
        }

        public void Update()
        {
            if (_mercComponent == null || !_mercComponent.IsHired || _state == JoinState.Complete)
                return;

            if (_mercComponent.FollowTarget == null)
                return;

            // Check if game is paused
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            // Don't update while performing an action (coroutine running)
            if (_isPerformingAction)
                return;

            // Don't update while moving
            if (_tileMover != null && _tileMover.IsMoving)
                return;

            // Don't update while jumping
            if (_jumpComponent != null && _jumpComponent.IsJumping)
                return;

            switch (_state)
            {
                case JoinState.PathfindingToTarget:
                    HandlePathfindingToTarget();
                    break;
                case JoinState.WalkingToPitEdge:
                    HandleWalkingToPitEdge();
                    break;
                case JoinState.JumpingIntoPit:
                    HandleJumpingIntoPit();
                    break;
            }
        }

        private void HandlePathfindingToTarget()
        {
            if (_pathfinding == null || !_pathfinding.IsPathfindingInitialized)
                return;

            // Get current positions
            var myTile = GetCurrentTile();
            var targetTile = GetTargetTile();

            // Update our last tile position for followers (but only if we're not disabled/frozen)
            // This prevents overwriting LastTilePosition during teleportation/repositioning
            if (_tileMover != null && _tileMover.Enabled && _mercComponent.LastTilePosition != myTile)
            {
                _mercComponent.LastTilePosition = myTile;
            }

            if (targetTile == _lastTargetTile && _currentPath != null && _currentPathIndex < _currentPath.Count)
            {
                // Continue following current path
                FollowPath();
                return;
            }

            // Check if both are in the same location (pit or outside)
            bool mercInPit = IsInsidePit(myTile);
            bool targetInPit = IsInsidePit(targetTile);

            if (mercInPit == targetInPit)
            {
                // Both in same area - direct pathfind
                _currentPath = _pathfinding.CalculatePath(myTile, targetTile);
                if (_currentPath != null && _currentPath.Count > 0)
                {
                    _currentPathIndex = 0;
                    _lastTargetTile = targetTile;
                    FollowPath();
                }
                else
                {
                    // Can't reach target, switch to follow mode
                    Debug.Log($"[MercenaryJoin] Cannot pathfind to target, switching to follow component");
                    SwitchToFollowMode();
                }
            }
            else if (!mercInPit && targetInPit)
            {
                // Merc outside, target inside - need to walk to pit edge then jump
                _pitEdgeTile = FindNearestPitEdge(myTile);
                _currentPath = _pathfinding.CalculatePath(myTile, _pitEdgeTile);
                if (_currentPath != null && _currentPath.Count > 0)
                {
                    _currentPathIndex = 0;
                    _state = JoinState.WalkingToPitEdge;
                    Debug.Log($"[MercenaryJoin] Walking to pit edge at ({_pitEdgeTile.X},{_pitEdgeTile.Y})");
                    FollowPath();
                }
                else
                {
                    Debug.Warn($"[MercenaryJoin] Cannot find path to pit edge");
                    SwitchToFollowMode();
                }
            }
            else
            {
                // Merc inside, target outside - direct pathfind (will handle jump out later if needed)
                _currentPath = _pathfinding.CalculatePath(myTile, targetTile);
                if (_currentPath != null && _currentPath.Count > 0)
                {
                    _currentPathIndex = 0;
                    _lastTargetTile = targetTile;
                    FollowPath();
                }
                else
                {
                    SwitchToFollowMode();
                }
            }
        }

        private void HandleWalkingToPitEdge()
        {
            var myTile = GetCurrentTile();

            // Update our last tile position for followers (but only if we're not disabled/frozen)
            // This prevents overwriting LastTilePosition during teleportation/repositioning
            if (_tileMover != null && _tileMover.Enabled && _mercComponent.LastTilePosition != myTile)
            {
                _mercComponent.LastTilePosition = myTile;
            }
            
            if (myTile == _pitEdgeTile)
            {
                // Reached pit edge, now jump
                Debug.Log($"[MercenaryJoin] Reached pit edge, jumping into pit");
                _state = JoinState.JumpingIntoPit;
                return;
            }

            FollowPath();
        }

        private void HandleJumpingIntoPit()
        {
            var myTile = GetCurrentTile();
            
            // Calculate jump target (2 tiles to the left, same Y)
            var jumpTarget = new Point(myTile.X - 2, myTile.Y);
            
            // Set flag to prevent state changes during jump
            _isPerformingAction = true;
            
            // Start jump coroutine that handles both movement and animation
            Core.StartCoroutine(PerformJumpIntoPit(jumpTarget));
            
            Debug.Log($"[MercenaryJoin] Started jump to ({jumpTarget.X},{jumpTarget.Y})");
        }

        private System.Collections.IEnumerator PerformJumpIntoPit(Point targetTile)
        {
            // Convert target tile to world position
            var targetPosition = new Vector2(
                targetTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                targetTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
            );

            var startPosition = Entity.Transform.Position;
            var distance = Vector2.Distance(startPosition, targetPosition);
            var duration = distance / (GameConfig.HeroJumpSpeed * GameConfig.TileSize);

            // Start jump animation
            if (_jumpComponent != null)
            {
                _jumpComponent.StartJump(Direction.Left, duration);
            }

            // Smoothly move entity to target
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.DeltaTime;
                var progress = elapsed / duration;
                Entity.Transform.Position = Vector2.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            // Ensure we're exactly at target
            Entity.Transform.Position = targetPosition;

            // Update triggers after teleport
            if (_tileMover != null)
            {
                _tileMover.UpdateTriggersAfterTeleport();
            }

            Debug.Log($"[MercenaryJoin] Jump complete, resuming pathfinding");
            _state = JoinState.PathfindingToTarget;
            _lastTargetTile = new Point(-1, -1); // Force recalculation
            _isPerformingAction = false; // Clear flag
        }

        private void FollowPath()
        {
            if (_currentPath == null || _currentPathIndex >= _currentPath.Count)
            {
                // Path complete, check if we need to continue
            var myTile = GetCurrentTile();
            var target = GetTargetTile();

            // If we're close enough (within 2 tiles), switch to follow mode
            var distance = System.Math.Abs(target.X - myTile.X) + System.Math.Abs(target.Y - myTile.Y);
            if (distance <= 2)
            {
                Debug.Log($"[MercenaryJoin] Close enough to target, switching to follow mode");
                SwitchToFollowMode();
                return;
            }

            // Otherwise recalculate path
            _lastTargetTile = new Point(-1, -1);
                return;
            }

            var targetTile = _currentPath[_currentPathIndex];
            var currentTile = GetCurrentTile();

            // Determine direction to move
            var dx = targetTile.X - currentTile.X;
            var dy = targetTile.Y - currentTile.Y;

            Direction? direction = null;
            if (dx > 0) direction = Direction.Right;
            else if (dx < 0) direction = Direction.Left;
            else if (dy > 0) direction = Direction.Down;
            else if (dy < 0) direction = Direction.Up;

            if (direction.HasValue && _tileMover != null)
            {
                if (_tileMover.StartMoving(direction.Value))
                {
                    _currentPathIndex++;
                }
            }
            else
            {
                // Already at target tile, skip to next
                _currentPathIndex++;
            }
        }

        private Point GetCurrentTile()
        {
            var pos = Entity.Transform.Position;
            return new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );
        }

        private Point GetTargetTile()
        {
            if (_mercComponent.FollowTarget == null)
                return GetCurrentTile();

            var targetPos = _mercComponent.FollowTarget.Transform.Position;
            return new Point(
                (int)(targetPos.X / GameConfig.TileSize),
                (int)(targetPos.Y / GameConfig.TileSize)
            );
        }

        private bool IsInsidePit(Point tile)
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return false;

            // Calculate pit bounds in tiles
            var pitLeft = GameConfig.PitRectX;
            var pitTop = GameConfig.PitRectY;
            var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
            var pitHeight = GameConfig.PitRectHeight;
            var pitRight = pitLeft + pitWidth - 1;
            var pitBottom = pitTop + pitHeight - 1;

            return tile.X >= pitLeft && tile.X <= pitRight && tile.Y >= pitTop && tile.Y <= pitBottom;
        }

        private Point FindNearestPitEdge(Point fromTile)
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return fromTile;

            // Calculate pit bounds in tiles
            var pitLeft = GameConfig.PitRectX;
            var pitTop = GameConfig.PitRectY;
            var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
            var pitHeight = GameConfig.PitRectHeight;
            var pitRight = pitLeft + pitWidth - 1;
            var pitBottom = pitTop + pitHeight - 1;
            
            // The pit edge jump-off position is always at the fixed center Y position
            // Only the X position is dynamic (based on pit width)
            var pitEdgeX = pitRight;
            var pitEdgeY = GameConfig.PitCenterTileY;

            return new Point(pitEdgeX, pitEdgeY);
        }

        private void SwitchToFollowMode()
        {
            Debug.Log($"[MercenaryJoin] Join complete, switching to follow mode");
            _state = JoinState.Complete;

            // Remove this component and add follow component
            if (!Entity.HasComponent<MercenaryFollowComponent>())
            {
                Entity.AddComponent(new MercenaryFollowComponent());
            }
            Entity.RemoveComponent(this);
        }

        /// <summary>
        /// Reset pathfinding state (used when mercenary is teleported)
        /// </summary>
        public void ResetPathfinding()
        {
            _currentPath = null;
            _currentPathIndex = 0;
            _lastTargetTile = new Point(-1, -1);
            _state = JoinState.PathfindingToTarget;
            _isPerformingAction = false;
            Debug.Log($"[MercenaryJoin] Pathfinding state reset for {Entity.Name}");
        }
    }
}
