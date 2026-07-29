using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the mercenary to jump into the pit from the pit edge
    /// </summary>
    public class MercenaryJumpIntoPitAction : MercenaryActionBase
    {
        private bool _isJumping = false;
        private bool _jumpFinished = false;
        private Point _plannedTargetTile;

        public MercenaryJumpIntoPitAction() : base(GoapConstants.MercenaryJumpIntoPitAction, 1)
        {
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            SetPrecondition(GoapConstants.MercenaryInsidePit, false);
            SetPrecondition(GoapConstants.TargetInsidePit, true);
            SetPrecondition(GoapConstants.MercenaryAtPitEdge, true);

            SetPostcondition(GoapConstants.MercenaryInsidePit, true);
            SetPostcondition(GoapConstants.MercenaryAtPitEdge, false);
            SetPostcondition(GoapConstants.MercenaryFollowingTarget, true);
        }

        /// <summary>A jump is a short atomic animation — never interrupt it mid-air (the movement
        /// coroutine cannot be cancelled and would fight any replanned movement). Only applies
        /// while airborne; a jump that has not started yet remains interruptible.</summary>
        public override bool ShouldNotOverride()
        {
            return _isJumping;
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
                    // Stale state from a jump that was interrupted mid-air (the coroutine finished
                    // but the mercenary has since been moved elsewhere). Waiting cannot resolve this —
                    // reset so the next Execute starts a fresh jump from the actual position.
                    Debug.Warn($"[MercenaryJumpIntoPit] Jump finished flag set but mercenary at {currentTile.X},{currentTile.Y} not at planned target {_plannedTargetTile.X},{_plannedTargetTile.Y}. Restarting jump from current tile.");
                    _isJumping = false;
                    _jumpFinished = false;
                    return false;
                }

                _isJumping = false;
                _jumpFinished = false;

                tileMover?.UpdateTriggersAfterTeleport();

                mercenary.InsidePit = true;

                Debug.Log($"[MercenaryJumpIntoPit] {mercenary.Entity.Name} jump completed successfully");
                return true;
            }

            var currentStartTile = mercenary.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates()
                ?? new Point((int)(mercenary.Entity.Transform.Position.X / GameConfig.TileSize),
                           (int)(mercenary.Entity.Transform.Position.Y / GameConfig.TileSize));

            var targetTile = CalculateJumpTargetTile(currentStartTile);
            if (!targetTile.HasValue)
            {
                Debug.Warn($"[MercenaryJumpIntoPit] {mercenary.Entity.Name} cannot calculate jump target tile");
                return true;
            }

            _plannedTargetTile = targetTile.Value;

            StartJumpMovement(mercenary, _plannedTargetTile);
            _isJumping = true;
            _jumpFinished = false;

            Debug.Log($"[MercenaryJumpIntoPit] {mercenary.Entity.Name} started jump to tile {_plannedTargetTile.X},{_plannedTargetTile.Y}");
            return false;
        }

        private Point? CalculateJumpTargetTile(Point currentTile)
        {
            return new Point(currentTile.X - 2, currentTile.Y);
        }

        private void StartJumpMovement(MercenaryComponent mercenary, Point targetTile)
        {
            var entity = mercenary.Entity;
            
            // CRITICAL: Snap to grid BEFORE calculating target position
            // This ensures we start from a perfectly aligned position
            var tileMover = entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
            }
            
            var targetPosition = TileToWorldPosition(targetTile);

            // Play jump sound effect
            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
            soundEffectManager.PlaySoundAt(SoundEffectType.Jump, entity.Transform.Position);

            Core.StartCoroutine(JumpMovementCoroutine(entity, targetPosition, GameConfig.HeroJumpSpeed));
        }

        private System.Collections.IEnumerator JumpMovementCoroutine(Entity entity, Vector2 targetPosition, float tilesPerSecond)
        {
            var startPosition = entity.Transform.Position;
            var distance = Vector2.Distance(startPosition, targetPosition);
            var duration = distance / (tilesPerSecond * GameConfig.TileSize);

            var jumpAnimComponent = entity.GetComponent<HeroJumpComponent>();
            if (jumpAnimComponent != null)
            {
                jumpAnimComponent.StartJump(Direction.Left, duration);
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

            // Refresh pathfinding to pick up collision layer plus all runtime obstacles
            var pathfinding = entity.GetComponent<PathfindingActorComponent>();
            if (pathfinding != null)
            {
                pathfinding.RefreshPathfindingWithObstacles();
                Debug.Log($"[MercenaryJumpIntoPit] Refreshed pathfinding for {entity.Name} after entering pit");
            }

            _jumpFinished = true;
            Debug.Log($"[MercenaryJumpIntoPit] Jump movement completed at {entity.Transform.Position.X},{entity.Transform.Position.Y}");

            // Play land sound effect
            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
            soundEffectManager.PlaySoundAt(SoundEffectType.Land, entity.Transform.Position);
        }
    }
}


