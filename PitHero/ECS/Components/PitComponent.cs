using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for pits in the game - logical entity with trigger collider
    /// </summary>
    public class PitComponent : Component, ITriggerListener
    {
        public float CrystalPower { get; set; } = 1f;
        public float EffectRadius { get; set; } = 100f;
        public bool IsActive { get; set; } = true;
        
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            // Initialize any pit-specific logic
        }

        /// <summary>
        /// Called when an entity enters the pit trigger area
        /// </summary>
        public void OnTriggerEnter(Collider other, Collider local)
        {
            // Check if it's the hero entering the pit area
            var heroComponent = other.Entity.GetComponent<HeroComponent>();
            if (heroComponent != null)
            {
                Debug.Log("[Pit] Hero entered pit trigger area");
                // The HeroComponent will handle its own state changes via its trigger listeners
            }
        }

        /// <summary>
        /// Called when an entity exits the pit trigger area
        /// </summary>
        public void OnTriggerExit(Collider other, Collider local)
        {
            // Check if it's the hero leaving the pit area
            var heroComponent = other.Entity.GetComponent<HeroComponent>();
            if (heroComponent != null)
            {
                Debug.Log("[Pit] Hero exited pit trigger area");
                // The HeroComponent will handle its own state changes via its trigger listeners
            }
        }
    }
}