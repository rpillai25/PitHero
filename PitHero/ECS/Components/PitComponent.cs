using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for pits in the game
    /// </summary>
    public class PitComponent : Component
    {
        public float CrystalPower { get; set; } = 1f;
        public float EffectRadius { get; set; } = 100f;
        public bool IsActive { get; set; } = true;
        
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            // Initialize any pit-specific logic
        }
    }
}