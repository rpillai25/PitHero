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
            // Precondition: Hero must be adjacent to pit boundary from outside
            SetPrecondition(GoapConstants.AdjacentToPitBoundaryFromOutside, true);
            
            // Postconditions: Hero enters pit
            SetPostcondition(GoapConstants.InsidePit, true);
            // Note: AdjacentToPitBoundaryFromInside is now reserved for a specific inside-edge coordinate and
            // is produced by MovingToInsidePitEdgeAction, not by this jump.
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
                // Ensure flags are correct on entry: no longer outside-adjacent and not considered inside-edge-adjacent yet
                hero.AdjacentToPitBoundaryFromInside = false;
                hero.AdjacentToPitBoundaryFromOutside = false;
                
                Debug.Log("[JumpIntoPit] Jump completed successfully");
                return true; // Action complete
            }

            // Start the jump
            if (!hero.AdjacentToPitBoundaryFromOutside || hero.PitApproachDirection == null)
            {
                Debug.Warn("[JumpIntoPit] Cannot jump - not adjacent to pit boundary from outside");
                return true; // Action failed, but complete
            }

            var targetTile = CalculateJumpTargetTile(hero);
            if (!targetTile.HasValue)
            {
                Debug.Warn("[JumpIntoPit] Cannot calculate jump target tile");
                return true; // Action failed, but complete
            }

            // Start the coroutine-based movement to avoid TileMap collider issues
            StartJumpMovement(hero, targetTile.Value);
            _isJumping = true;
            
            Debug.Log($"[JumpIntoPit] Started jump from direction {hero.PitApproachDirection} to tile {targetTile.Value.X},{targetTile.Value.Y}");
            return false; // Action in progress
        }

        /// <summary>
        /// Calculate the target tile based on approach direction
        /// </summary>
        private Point? CalculateJumpTargetTile(HeroComponent hero)
        {
            var direction = hero.PitApproachDirection.Value;
            var currentTile = hero.Entity.GetComponent<TileByTileMover>()?.GetCurrentTileCoordinates() 
                ?? new Point((int)(hero.Entity.Transform.Position.X / GameConfig.TileSize), 
                           (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize));

            switch (direction)
            {
                case Direction.Right: // Approaching from left, move 2 tiles right
                    return new Point(currentTile.X + 2, currentTile.Y);
                    
                case Direction.Left: // Approaching from right, move 2 tiles left
                    return new Point(currentTile.X - 2, currentTile.Y);
                    
                case Direction.Down: // Approaching from above, move 2 tiles down
                    return new Point(currentTile.X, currentTile.Y + 2);
                    
                case Direction.Up: // Approaching from below, move 2 tiles up
                    return new Point(currentTile.X, currentTile.Y - 2);
                    
                // Corner cases
                case Direction.DownRight: // Upper left corner (1,2) -> (2,3)
                    return new Point(2, 3);
                    
                case Direction.DownLeft: // Upper right corner (12,2) -> (11,3)
                    return new Point(11, 3);
                    
                case Direction.UpLeft: // Lower right corner (12,10) -> (11,9)
                    return new Point(11, 9);
                    
                case Direction.UpRight: // Lower left corner (1,10) -> (2,9)
                    return new Point(2, 9);
                    
                default:
                    return null;
            }
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