using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
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
}