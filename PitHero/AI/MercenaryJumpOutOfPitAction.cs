using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;

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

                tileMover?.UpdateTriggersAfterTeleport();

                Debug.Log($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} jump out completed successfully");
                return true;
            }

            var targetTile = CalculateJumpOutTargetTile(mercenary);
            if (!targetTile.HasValue)
            {
                Debug.Warn($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} cannot calculate jump out target tile");
                return true;
            }

            _plannedTargetTile = targetTile.Value;

            StartJumpOutMovement(mercenary, _plannedTargetTile);
            _isJumping = true;
            _jumpFinished = false;

            Debug.Log($"[MercenaryJumpOutOfPit] {mercenary.Entity.Name} started jump out to tile {_plannedTargetTile.X},{_plannedTargetTile.Y}");
            return false;
        }

        private Point? CalculateJumpOutTargetTile(MercenaryComponent mercenary)
        {
            var currentTile = mercenary.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates()
                ?? new Point((int)(mercenary.Entity.Transform.Position.X / GameConfig.TileSize),
                           (int)(mercenary.Entity.Transform.Position.Y / GameConfig.TileSize));

            var targetTile = new Point(currentTile.X + 2, currentTile.Y);

            Debug.Log($"[MercenaryJumpOutOfPit] Calculated jump out target from {currentTile.X},{currentTile.Y} to {targetTile.X},{targetTile.Y}");
            return targetTile;
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
