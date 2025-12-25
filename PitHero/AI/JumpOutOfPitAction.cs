using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;
using System.Collections;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to jump out of the pit when ready to jump out
    /// This is the reverse of JumpIntoPit - hero jumps from inside boundary to outside
    /// </summary>
    public class JumpOutOfPitAction : HeroActionBase
    {
        private bool _isJumping = false;
        private bool _jumpFinished = false;
        private Point _plannedTargetTile;

        public JumpOutOfPitAction() : base(GoapConstants.JumpOutOfPitAction)
        {
            // Preconditions: Hero must be inside pit and have critical HP
            SetPrecondition(GoapConstants.InsidePit, true);
            SetPrecondition(GoapConstants.HPCritical, true);

            // Postcondition: Hero is outside pit
            SetPostcondition(GoapConstants.OutsidePit, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // If already jumping, check if movement is complete
            if (_isJumping)
            {
                if (!_jumpFinished)
                {
                    return false; // Still moving, action not complete
                }

                // Verify we actually reached the intended tile before completing
                var tileMover = hero.Entity.GetComponent<TileByTileMover>();
                var currentTile = tileMover?.GetCurrentTileCoordinates()
                    ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize),
                               (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

                if (currentTile.X != _plannedTargetTile.X || currentTile.Y != _plannedTargetTile.Y)
                {
                    Debug.Warn($"[JumpOutOfPit] Jump finished flag set but hero at {currentTile.X},{currentTile.Y} not at planned target {_plannedTargetTile.X},{_plannedTargetTile.Y}. Waiting one more frame.");
                    return false;
                }

                // Movement complete, finalize the jump
                _isJumping = false;
                _jumpFinished = false;

                // Ensure triggers update so pit exit is registered
                tileMover?.UpdateTriggersAfterTeleport();

                hero.InsidePit = false;  // Set InsidePit = False according to specification

                Debug.Log("[JumpOutOfPit] Jump out completed successfully");
                return true; // Action complete
            }

            // Start the jump out
            var targetTile = CalculateJumpOutTargetTile(hero);
            if (!targetTile.HasValue)
            {
                Debug.Warn("[JumpOutOfPit] Cannot calculate jump out target tile");
                return true; // Action failed, but complete
            }

            _plannedTargetTile = targetTile.Value;

            // Start the coroutine-based movement to avoid TileMap collider issues
            StartJumpOutMovement(hero, _plannedTargetTile);
            _isJumping = true;
            _jumpFinished = false;

            Debug.Log($"[JumpOutOfPit] Started jump out to tile {_plannedTargetTile.X},{_plannedTargetTile.Y}");
            return false; // Action in progress
        }

        /// <summary>
        /// Calculate the target tile for jumping out (2 tiles to the right from current position)
        /// </summary>
        private Point? CalculateJumpOutTargetTile(HeroComponent hero)
        {
            var currentTile = hero.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates()
                ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize),
                           (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            // Jump out 2 tiles to the right (reverse of jumping in from the right)
            var targetTile = new Point(currentTile.X + 2, currentTile.Y);

            Debug.Log($"[JumpOutOfPit] Calculated jump out target from {currentTile.X},{currentTile.Y} to {targetTile.X},{targetTile.Y}");
            return targetTile;
        }

        /// <summary>
        /// Start coroutine-based movement to target tile
        /// </summary>
        private void StartJumpOutMovement(HeroComponent hero, Point targetTile)
        {
            var targetPosition = TileToWorldPosition(targetTile);
            var entity = hero.Entity;

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
            var duration = distance / (tilesPerSecond * GameConfig.TileSize); // Convert tiles per second to pixels per second

            // Start jump animation - determine direction from start to target
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

                // Smooth interpolation
                entity.Transform.Position = Vector2.Lerp(startPosition, targetPosition, progress);

                yield return null; // Wait for next frame
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
                // Force trigger update so the pit exit trigger updates immediately
                tileMover.UpdateTriggersAfterTeleport();
            }

            // Clear fog of war around the landing position
            var hero = entity.GetComponent<HeroComponent>();
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            bool fogCleared = tiledMapService?.ClearFogOfWarAroundTile(
                (int)(targetPosition.X / GameConfig.TileSize),
                (int)(targetPosition.Y / GameConfig.TileSize), hero
            ) ?? false;

            // Trigger fog cooldown if fog was cleared
            if (fogCleared)
            {
                var heroComponent = entity.GetComponent<HeroComponent>();
                heroComponent?.TriggerFogCooldown();
            }

            _jumpFinished = true;

            Debug.Log($"[JumpOutOfPit] Jump out movement completed at {entity.Transform.Position.X},{entity.Transform.Position.Y}");
        }

        /// <summary>
        /// Determine jump direction based on start and target positions
        /// </summary>
        private Direction GetJumpDirection(Vector2 startPosition, Vector2 targetPosition)
        {
            var delta = targetPosition - startPosition;

            // Since we're jumping out of the pit to the right, this should be right
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