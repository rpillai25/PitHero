using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Action that moves the hero left continuously using timed tile-based movement
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

            // If not currently moving, try to start moving left
            if (!tileMover.IsMoving)
            {
                bool moveStarted = tileMover.StartMoving(Direction.Left);
                
                if (!moveStarted)
                {
                    Debug.Log("[MoveLeft] Movement blocked - collision detected");
                    // Movement was blocked, action is complete
                    return true;
                }
                
                Debug.Log("[MoveLeft] Started moving left");
            }
            
            // Action continues as long as we're moving left
            // This allows the movement to complete over multiple frames
            return !tileMover.IsMoving || tileMover.CurrentDirection != Direction.Left;
        }
    }
}