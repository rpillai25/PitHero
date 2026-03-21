using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using System.Collections;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to jump out of the pit when the player has stopped adventuring.
    /// Logic is identical to JumpOutOfPitForInnAction but triggered by StoppedAdventure state.
    /// </summary>
    public class JumpOutOfPitForStopAction : HeroActionBase
    {
        private bool _isJumping = false;
        private bool _jumpFinished = false;
        private Point _plannedTargetTile;

        public JumpOutOfPitForStopAction() : base(GoapConstants.JumpOutOfPitForStopAction, 99)
        {
            SetPrecondition(GoapConstants.InsidePit, true);
            SetPrecondition(GoapConstants.StoppedAdventure, true);

            SetPostcondition(GoapConstants.OutsidePit, true);
        }

        /// <summary>
        /// Execute action - jump hero out of the pit
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // If already jumping, check if movement is complete
            if (_isJumping)
            {
                if (!_jumpFinished)
                {
                    return false;
                }

                // Verify we actually reached the intended tile before completing
                var tileMover = hero.Entity.GetComponent<TileByTileMover>();
                var currentTile = tileMover?.GetCurrentTileCoordinates()
                    ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize),
                               (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

                if (currentTile.X != _plannedTargetTile.X || currentTile.Y != _plannedTargetTile.Y)
                {
                    Debug.Warn($"[JumpOutOfPitForStop] Jump finished flag set but hero at {currentTile.X},{currentTile.Y} not at planned target {_plannedTargetTile.X},{_plannedTargetTile.Y}. Waiting one more frame.");
                    return false;
                }

                // Movement complete, finalize the jump
                _isJumping = false;
                _jumpFinished = false;

                // Ensure triggers update so pit exit is registered
                tileMover?.UpdateTriggersAfterTeleport();

                Debug.Log("[JumpOutOfPitForStop] Jump out completed successfully");
                return true;
            }

            // Start the jump out
            var targetTile = CalculateJumpOutTargetTile(hero);
            if (!targetTile.HasValue)
            {
                Debug.Warn("[JumpOutOfPitForStop] Cannot calculate jump out target tile");
                return true;
            }

            _plannedTargetTile = targetTile.Value;

            // Update InsidePit flag BEFORE starting movement
            hero.InsidePit = false;

            // Start the coroutine-based movement
            StartJumpOutMovement(hero, _plannedTargetTile);
            _isJumping = true;
            _jumpFinished = false;

            Debug.Log($"[JumpOutOfPitForStop] Started jump out to tile {_plannedTargetTile.X},{_plannedTargetTile.Y}, set InsidePit=false");
            return false;
        }

        /// <summary>
        /// Calculate the target tile for jumping out (2 tiles to the right from current position)
        /// </summary>
        private Point? CalculateJumpOutTargetTile(HeroComponent hero)
        {
            var currentTile = hero.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates()
                ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize),
                           (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            var targetTile = new Point(currentTile.X + 2, currentTile.Y);

            Debug.Log($"[JumpOutOfPitForStop] Calculated jump out target from {currentTile.X},{currentTile.Y} to {targetTile.X},{targetTile.Y}");
            return targetTile;
        }

        /// <summary>
        /// Start coroutine-based movement to target tile
        /// </summary>
        private void StartJumpOutMovement(HeroComponent hero, Point targetTile)
        {
            var entity = hero.Entity;

            // Snap to grid BEFORE calculating target position
            var tileMover = entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
            }

            var targetPosition = TileToWorldPosition(targetTile);

            // Play jump sound effect
            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
            soundEffectManager.PlaySound(SoundEffectType.Jump);

            // Start the movement coroutine
            Core.StartCoroutine(JumpOutMovementCoroutine(entity, targetPosition, GameConfig.HeroJumpSpeed));
        }

        /// <summary>
        /// Coroutine that smoothly moves the entity to target position over time
        /// </summary>
        private IEnumerator JumpOutMovementCoroutine(Entity entity, Vector2 targetPosition, float tilesPerSecond)
        {
            var startPosition = entity.Transform.Position;
            var distance = Vector2.Distance(startPosition, targetPosition);
            var duration = distance / (tilesPerSecond * GameConfig.TileSize);

            // Start jump animation
            var jumpDirection = GetJumpDirection(startPosition, targetPosition);
            var jumpAnimComponent = entity.GetComponent<HeroJumpComponent>();
            if (jumpAnimComponent != null)
            {
                jumpAnimComponent.StartJump(jumpDirection, duration);
            }

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.DeltaTime;
                var progress = elapsed / duration;

                entity.Transform.Position = Vector2.Lerp(startPosition, targetPosition, progress);

                yield return null;
            }

            // Ensure we end exactly at target
            entity.Transform.Position = targetPosition;

            // End jump animation
            if (jumpAnimComponent != null)
            {
                jumpAnimComponent.EndJump();
            }

            // Snap to tile grid for precision
            var tileMover = entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
                tileMover.UpdateTriggersAfterTeleport();
            }

            // Clear fog of war around the landing position
            var hero = entity.GetComponent<HeroComponent>();
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            bool fogCleared = tiledMapService?.ClearFogOfWarAroundTile(
                (int)(targetPosition.X / GameConfig.TileSize),
                (int)(targetPosition.Y / GameConfig.TileSize), hero
            ) ?? false;

            if (fogCleared)
            {
                var heroComponent = entity.GetComponent<HeroComponent>();
                heroComponent?.TriggerFogCooldown();
            }

            _jumpFinished = true;

            Debug.Log($"[JumpOutOfPitForStop] Jump out movement completed at {entity.Transform.Position.X},{entity.Transform.Position.Y}");
        }

        /// <summary>
        /// Determine jump direction based on start and target positions
        /// </summary>
        private Direction GetJumpDirection(Vector2 startPosition, Vector2 targetPosition)
        {
            var delta = targetPosition - startPosition;

            if (System.Math.Abs(delta.X) > System.Math.Abs(delta.Y))
            {
                return delta.X > 0 ? Direction.Right : Direction.Left;
            }
            else
            {
                return delta.Y > 0 ? Direction.Down : Direction.Up;
            }
        }
    }
}
