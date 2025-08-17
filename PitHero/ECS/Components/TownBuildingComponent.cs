using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for town buildings
    /// </summary>
    public class TownBuildingComponent : Component
    {
        public string BuildingType { get; set; }
        public float EffectRadius { get; set; } = 50f;
        public bool IsConstructed { get; set; } = true;
        
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            // Initialize any building-specific logic
        }
    }
}