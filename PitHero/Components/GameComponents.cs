using Microsoft.Xna.Framework;
using PitHero.ECS;

namespace PitHero.Components
{
    /// <summary>
    /// Component for heroes in the game
    /// </summary>
    public class HeroComponent : Component
    {
        public float Health { get; set; } = 100f;
        public float MaxHealth { get; set; } = 100f;
        public float MoveSpeed { get; set; } = GameConfig.HeroMoveSpeed;
        public Vector2 Velocity { get; set; }
        public bool IsAlive => Health > 0f;
        
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            // Initialize any hero-specific logic
        }
    }
    
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
    
    /// <summary>
    /// Component for renderable objects
    /// </summary>
    public class RenderComponent : Component
    {
        public Color Color { get; set; } = Color.White;
        public int Width { get; set; } = 32;
        public int Height { get; set; } = 32;
        public int ZOrder { get; set; } = 0;
        
        public Rectangle Bounds => new Rectangle(
            (int)Entity.Position.X,
            (int)Entity.Position.Y,
            Width,
            Height
        );
    }
}