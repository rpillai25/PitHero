using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Simple test action that moves the hero left indefinitely using TiledMapMover for collision handling
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
            // Get the TiledMapMover component from the hero entity
            var tiledMover = hero.Entity.GetComponent<TiledMapMover>();
            var boxCollider = hero.Entity.GetComponent<BoxCollider>();
            
            if (tiledMover == null || boxCollider == null)
            {
                Debug.Warn("MoveLeftAction: Hero entity missing TiledMapMover or BoxCollider component");
                return false;
            }

            // Create movement vector pointing left
            var movement = new Vector2(-hero.MoveSpeed * Time.DeltaTime, 0);
            
            // Create collision state for the TiledMapMover
            var collisionState = new TiledMapMover.CollisionState();
            
            // Use TiledMapMover to move the hero left with collision detection
            tiledMover.Move(movement, boxCollider, collisionState);
            
            // Log collision info for debugging
            if (collisionState.HasCollision)
            {
                Debug.Log($"[MoveLeft] Collision detected: Left={collisionState.Left}, Right={collisionState.Right}, Above={collisionState.Above}, Below={collisionState.Below}");
            }
            
            // This action continues indefinitely (never returns true)
            // The hero will keep moving left until stopped by collision
            return false;
        }
    }
}