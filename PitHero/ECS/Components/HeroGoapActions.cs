using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Simple test action that moves the hero left one tile at a time using TileByTileMover
    /// </summary>
    public class MoveLeftAction : HeroActionBase
    {
        public MoveLeftAction() : base("MoveLeft", 1)
        {
            // Only precondition is that the hero entity has been initialized
            SetPrecondition("HeroInitialized", true);
            
            // Postcondition is that the hero is moving left
            SetPostcondition("MovingLeft", true);
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

            // Check if the mover is currently moving (prevents overlapping movements)
            if (tileMover.IsMoving)
            {
                // Still moving, action not complete
                return false;
            }

            // Attempt to move one tile to the left
            bool moveSuccessful = tileMover.TryMoveInDirection(Direction.Left);
            
            if (!moveSuccessful)
            {
                Debug.Log("[MoveLeft] Movement blocked - collision detected or invalid move");
                // Movement was blocked, but action is considered complete
                return true;
            }
            
            Debug.Log("[MoveLeft] Successfully moved one tile left");
            
            // For now, this action completes after one tile movement
            // In the future, this could be modified to continue moving until blocked
            return true;
        }
    }
}