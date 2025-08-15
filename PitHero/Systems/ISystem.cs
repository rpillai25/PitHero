using PitHero.ECS;
using PitHero.Events;

namespace PitHero.Systems
{
    /// <summary>
    /// Base interface for all systems in the ECS architecture
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// Whether this system is enabled
        /// </summary>
        bool Enabled { get; set; }
        
        /// <summary>
        /// Update the system with the current world state and delta time
        /// </summary>
        void Update(WorldState worldState, float deltaTime);
        
        /// <summary>
        /// Process an event in this system
        /// </summary>
        void ProcessEvent(IEvent gameEvent, WorldState worldState);
    }
    
    /// <summary>
    /// Base implementation for systems
    /// </summary>
    public abstract class BaseSystem : ISystem
    {
        public bool Enabled { get; set; } = true;
        
        public virtual void Update(WorldState worldState, float deltaTime)
        {
            if (!Enabled) return;
            
            OnUpdate(worldState, deltaTime);
        }
        
        public virtual void ProcessEvent(IEvent gameEvent, WorldState worldState)
        {
            if (!Enabled) return;
            
            OnProcessEvent(gameEvent, worldState);
        }
        
        /// <summary>
        /// Override this to implement system-specific update logic
        /// </summary>
        protected virtual void OnUpdate(WorldState worldState, float deltaTime) { }
        
        /// <summary>
        /// Override this to implement system-specific event processing
        /// </summary>
        protected virtual void OnProcessEvent(IEvent gameEvent, WorldState worldState) { }
    }
}