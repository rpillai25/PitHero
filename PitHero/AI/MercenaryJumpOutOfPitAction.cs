using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the mercenary to jump out of the pit when target is outside
    /// </summary>
    public class MercenaryJumpOutOfPitAction : MercenaryActionBase
    {
        private bool _isJumping = false;
        private bool _jumpFinished = false;
        private Point _plannedTargetTile;
        private List<Point> _pathToJumpPosition;
        private int _pathIndex;

        public MercenaryJumpOutOfPitAction() : base(GoapConstants.MercenaryJumpOutOfPitAction, 1)
        {
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            SetPrecondition(GoapConstants.MercenaryInsidePit, true);
            SetPrecondition(GoapConstants.TargetInsidePit, false);

            SetPostcondition(GoapConstants.MercenaryInsidePit, false);
            SetPostcondition(GoapConstants.MercenaryFollowingTarget, true);
        }

        public override bool Execute(MercenaryComponent mercenary)
        {
            if (_isJumping)
            {
                if (!_jumpFinished)
                    return false;

                var tileMover = mercenary.Entity.GetComponent<TileByTileMover>();
                var currentTile = tileMover?.GetCurrentTileCoordinates()
                    ?? new Point((int)(mercenary.Entity.Transform.Position.X / GameConfig.TileSize),
                               (int)(mercenary.Entity.Transform.Position.Y / GameConfig.TileSize));

                if (currentTile.X != _plannedTargetTile.X || currentTile.Y != _plannedTargetTile.Y)
                {
                    Debug.Warn($"[MercenaryJumpOutOfPit] Jump finished flag set but mercenary at {currentTile.X},{currentTile.Y} not at planned target {_plannedTargetTile.X},{_plannedTargetTile.Y}. Waiting one more frame.");
                    return false;
                }

                _isJumping = false;
                _jumpFinished = false;
                _pathToJumpPosition = null;
                _pathIndex = 0;

                tileMover?.UpdateTriggersAfterTeleport();

                Debug.Log($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} jump out completed successfully");
                return true;
            }

            // First, check if mercenary is at the correct jump position (pit inside edge)
            var tileMover2 = mercenary.Entity.GetComponent<TileByTileMover>();
            var currentTile2 = tileMover2?.GetCurrentTileCoordinates()
                ?? new Point((int)(mercenary.Entity.Transform.Position.X / GameConfig.TileSize),
                           (int)(mercenary.Entity.Transform.Position.Y / GameConfig.TileSize));

            var jumpPosition = CalculatePitInsideEdgeLocation();
            if (!jumpPosition.HasValue)
            {
                Debug.Warn($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} cannot calculate pit inside edge location");
                return true;
            }

            // If not at jump position, walk to it first
            if (currentTile2.X != jumpPosition.Value.X || currentTile2.Y != jumpPosition.Value.Y)
            {
                return WalkToJumpPosition(mercenary, currentTile2, jumpPosition.Value);
            }

            // At jump position, calculate jump out target and execute jump
            var targetTile = CalculateJumpOutTargetTile(jumpPosition.Value);
            if (!targetTile.HasValue)
            {
                Debug.Warn($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} cannot calculate jump out target tile");
                return true;
            }

            _plannedTargetTile = targetTile.Value;

            StartJumpOutMovement(mercenary, _plannedTargetTile);
            _isJumping = true;
            _jumpFinished = false;

            Debug.Log($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} started jump out from ({jumpPosition.Value.X},{jumpPosition.Value.Y}) to tile ({_plannedTargetTile.X},{_plannedTargetTile.Y})");
            return false;
        }

        /// <summary>
        /// Calculate the pit inside edge location - same logic as HeroStateMachine
        /// </summary>
        private Point? CalculatePitInsideEdgeLocation()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var pitRightEdge = pitWidthManager?.CurrentPitRightEdge ?? (GameConfig.PitRectX + GameConfig.PitRectWidth);

            return new Point(pitRightEdge - 2, GameConfig.PitCenterTileY);
        }

        /// <summary>
        /// Calculate jump out target tile from the jump position
        /// </summary>
        private Point? CalculateJumpOutTargetTile(Point jumpPosition)
        {
            var targetTile = new Point(jumpPosition.X + 2, jumpPosition.Y);

            Debug.Log($"[MercenaryJumpOutOfPit] Calculated jump out target from ({jumpPosition.X},{jumpPosition.Y}) to ({targetTile.X},{targetTile.Y})");
            return targetTile;
        }

        /// <summary>
        /// Walk to the jump position before jumping out
        /// </summary>
        private bool WalkToJumpPosition(MercenaryComponent mercenary, Point currentTile, Point jumpPosition)
        {
            var tileMover = mercenary.Entity.GetComponent<TileByTileMover>();
            if (tileMover == null)
            {
                Debug.Warn($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} has no TileByTileMover");
                return true;
            }

            // If already moving, wait for movement to complete
            if (tileMover.IsMoving)
            {
                return false;
            }

            // Initialize path if needed
            if (_pathToJumpPosition == null)
            {
                var pathfinding = mercenary.Entity.GetComponent<PathfindingActorComponent>();
                if (pathfinding == null || !pathfinding.IsPathfindingInitialized)
                {
                    Debug.Warn($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} pathfinding not initialized");
                    return true;
                }

                _pathToJumpPosition = pathfinding.CalculatePath(currentTile, jumpPosition);
                _pathIndex = 0;

                if (_pathToJumpPosition == null || _pathToJumpPosition.Count == 0)
                {
                    Debug.Warn($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} cannot find path from ({currentTile.X},{currentTile.Y}) to jump position ({jumpPosition.X},{jumpPosition.Y})");
                    return true;
                }

                Debug.Log($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} walking to jump position ({jumpPosition.X},{jumpPosition.Y}), path length: {_pathToJumpPosition.Count}");
            }

            // Follow the path
            if (_pathIndex >= _pathToJumpPosition.Count)
            {
                // Reached jump position
                Debug.Log($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} reached jump position ({jumpPosition.X},{jumpPosition.Y})");
                _pathToJumpPosition = null;
                _pathIndex = 0;
                return false; // Continue to jump execution on next tick
            }

            var nextTile = _pathToJumpPosition[_pathIndex];
            var direction = CalculateDirection(currentTile, nextTile);

            if (direction.HasValue)
            {
                if (tileMover.StartMoving(direction.Value))
                {
                    _pathIndex++;
                }
                else
                {
                    Debug.Warn($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} failed to start moving {direction.Value}");
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate direction from current tile to next tile
        /// </summary>
        private Direction? CalculateDirection(Point from, Point to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;

            if (dx == 0 && dy == -1) return Direction.Up;
            if (dx == 0 && dy == 1) return Direction.Down;
            if (dx == -1 && dy == 0) return Direction.Left;
            if (dx == 1 && dy == 0) return Direction.Right;

            // Handle multi-tile movements by picking primary direction
            if (System.Math.Abs(dx) > System.Math.Abs(dy))
                return dx > 0 ? Direction.Right : Direction.Left;
            else if (dy != 0)
                return dy > 0 ? Direction.Down : Direction.Up;

            return null;
        }

        private void StartJumpOutMovement(MercenaryComponent mercenary, Point targetTile)
        {
            var targetPosition = TileToWorldPosition(targetTile);
            var entity = mercenary.Entity;

            Core.StartCoroutine(JumpOutMovementCoroutine(entity, targetPosition, GameConfig.HeroJumpSpeed));
        }

        private System.Collections.IEnumerator JumpOutMovementCoroutine(Entity entity, Vector2 targetPosition, float tilesPerSecond)
        {
            var startPosition = entity.Transform.Position;
            var distance = Vector2.Distance(startPosition, targetPosition);
            var duration = distance / (tilesPerSecond * GameConfig.TileSize);

            var jumpAnimComponent = entity.GetComponent<HeroJumpComponent>();
            if (jumpAnimComponent != null)
            {
                jumpAnimComponent.StartJump(Direction.Right, duration);
            }

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.DeltaTime;
                var progress = elapsed / duration;

                entity.Transform.Position = Vector2.Lerp(startPosition, targetPosition, progress);

                yield return null;
            }

            entity.Transform.Position = targetPosition;

            if (jumpAnimComponent != null)
            {
                jumpAnimComponent.EndJump();
            }

            var tileMover = entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
                tileMover.UpdateTriggersAfterTeleport();
            }

            // Mercenaries do not clear fog of war - only heroes can

            _jumpFinished = true;

            Debug.Log($"[MercenaryJumpOutOfPit] Jump out movement completed at {entity.Transform.Position.X},{entity.Transform.Position.Y}");
        }
    }
}
