using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;

namespace PitHero.AI
{
    /// <summary>
    /// Action that moves the hero left until blocked or reaches pit boundary
    /// </summary>
    public class MoveLeftAction : HeroActionBase
    {
        public MoveLeftAction() : base("MoveLeft", 1)
        {
            // Precondition: Hero must be initialized and NOT already at pit boundary
            SetPrecondition("HeroInitialized", true);
            
            // Postcondition: Hero will be adjacent to pit boundary from outside
            SetPostcondition("AdjacentToPitBoundaryFromOutside", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            // Get the TileByTileMover component from the hero entity
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            
            if (tileMover == null)
            {
                Debug.Warn("MoveLeftAction: Hero entity missing TileByTileMover component");
                return false;
            }

            // Check if we're already at the pit boundary from outside
            if (hero.AdjacentToPitBoundaryFromOutside)
            {
                Debug.Log("[MoveLeft] Already at pit boundary from outside - action complete");
                return true;
            }

            // If not currently moving, try to start moving left
            if (!tileMover.IsMoving)
            {
                bool moveStarted = tileMover.StartMoving(Direction.Left);
                
                if (!moveStarted)
                {
                    Debug.Log("[MoveLeft] Movement blocked - checking if we've reached pit boundary");
                    
                    // Movement blocked - check if we've reached the pit boundary
                    // This is a simple check - in a real game you'd want more sophisticated pathfinding
                    var currentTile = tileMover.GetCurrentTileCoordinates();
                    var pitBounds = new Rectangle(
                        GameConfig.PitRectX,
                        GameConfig.PitRectY,
                        GameConfig.PitRectWidth,
                        GameConfig.PitRectHeight
                    );
                    
                    // Check if we're adjacent to the left side of the pit
                    if (currentTile.X == pitBounds.Left - 1 && 
                        currentTile.Y >= pitBounds.Top && 
                        currentTile.Y < pitBounds.Bottom)
                    {
                        // We've reached the pit boundary from the left side
                        hero.AdjacentToPitBoundaryFromOutside = true;
                        hero.PitApproachDirection = Direction.Right; // Approaching from left, will move right into pit
                        Debug.Log($"[MoveLeft] Reached pit boundary at tile {currentTile}");
                        return true;
                    }
                    
                    // Movement blocked but not at pit - action failed
                    Debug.Log("[MoveLeft] Movement blocked and not at pit boundary - action failed");
                    return true; // Complete the action as failed
                }
                
                Debug.Log("[MoveLeft] Started moving left");
            }
            
            // Action continues as long as we're moving left and haven't reached the boundary
            // The movement will eventually either be blocked (triggering the check above) or we'll reach the pit trigger
            return false; // Action still in progress
        }
    }
}