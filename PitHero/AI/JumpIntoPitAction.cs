using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to jump into the pit when adjacent to pit boundary from outside
    /// </summary>
    public class JumpIntoPitAction : HeroActionBase
    {
        private bool _isJumping = false;
        
        public JumpIntoPitAction() : base(GoapConstants.JumpIntoPitAction, 1)
        {
            // Precondition: Hero and pit must be initialized
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            
            // Postcondition: Hero enters pit
            SetPostcondition(GoapConstants.InsidePit, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // If already jumping, check if movement is complete
            if (_isJumping)
            {
                var tileMover = hero.Entity.GetComponent<TileByTileMover>();
                if (tileMover != null && tileMover.IsMoving)
                {
                    return false; // Still moving, action not complete
                }
                
                // Movement complete, finalize the jump
                _isJumping = false;
                hero.InsidePit = true;
                
                Debug.Log("[JumpIntoPit] Jump completed successfully");
                return true; // Action complete
            }

            // Start the jump - calculate target based on pit bounds
            var currentTile = hero.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates() 
                ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize), 
                           (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            var targetTile = CalculateJumpTargetTile(currentTile);
            if (!targetTile.HasValue)
            {
                Debug.Warn("[JumpIntoPit] Cannot calculate jump target tile");
                return true; // Action failed, but complete
            }

            // Start the coroutine-based movement to avoid TileMap collider issues
            StartJumpMovement(hero, targetTile.Value);
            _isJumping = true;
            
            Debug.Log($"[JumpIntoPit] Started jump to tile {targetTile.Value.X},{targetTile.Value.Y}");
            return false; // Action in progress
        }

        /// <summary>
        /// Calculate the target tile based on current position and pit bounds
        /// </summary>
        private Point? CalculateJumpTargetTile(Point currentTile)
        {
            // Jump into the pit interior - target a safe tile inside the pit
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            
            // Calculate pit bounds
            int pitLeftX = GameConfig.PitRectX + 1;  // Inside left wall
            int pitRightX = (pitWidthManager?.CurrentPitRightEdge ?? (GameConfig.PitRectX + GameConfig.PitRectWidth)) - 2;  // Inside right wall
            int pitTopY = GameConfig.PitRectY + 1;   // Inside top wall
            int pitBottomY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2;  // Inside bottom wall
            
            // Choose a safe landing spot in the middle of the pit
            int targetX = (pitLeftX + pitRightX) / 2;
            int targetY = (pitTopY + pitBottomY) / 2;
            
            return new Point(targetX, targetY);
        }

        /// <summary>
        /// Start coroutine-based movement to target tile
        /// </summary>
        private void StartJumpMovement(HeroComponent hero, Point targetTile)
        {
            var targetPosition = TileToWorldPosition(targetTile);
            var entity = hero.Entity;
            
            // Start the movement coroutine
            Core.StartCoroutine(JumpMovementCoroutine(entity, targetPosition, GameConfig.HeroJumpSpeed));
        }

        /// <summary>
        /// Coroutine that smoothly moves the entity to target position over time
        /// </summary>
        private System.Collections.IEnumerator JumpMovementCoroutine(Entity entity, Vector2 targetPosition, float tilesPerSecond)
        {
            var startPosition = entity.Transform.Position;
            var distance = Vector2.Distance(startPosition, targetPosition);
            var duration = distance / (tilesPerSecond * GameConfig.TileSize); // Convert tiles per second to pixels per second
            
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
            
            // Snap to tile grid for precision
            var tileMover = entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
            }

            var tiledMapService = Core.Services.GetService<TiledMapService>();
            tiledMapService.ClearFogOfWarAroundTile(
                (int)(targetPosition.X / GameConfig.TileSize),
                (int)(targetPosition.Y / GameConfig.TileSize)
            );


            Debug.Log($"[JumpIntoPit] Jump movement completed at {entity.Transform.Position.X},{entity.Transform.Position.Y}");
        }
    }
}