using System;

namespace PitHero.Events
{
    /// <summary>
    /// Base interface for all events in the game
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// Unique identifier for this event
        /// </summary>
        Guid Id { get; }
        
        /// <summary>
        /// Timestamp when the event was created
        /// </summary>
        DateTime Timestamp { get; }
        
        /// <summary>
        /// Game time when the event occurred (for deterministic replay)
        /// </summary>
        double GameTime { get; }
    }
    
    /// <summary>
    /// Base implementation for all events
    /// </summary>
    public abstract class BaseEvent : IEvent
    {
        public Guid Id { get; }
        public DateTime Timestamp { get; }
        public double GameTime { get; }
        
        protected BaseEvent(double gameTime)
        {
            Id = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            GameTime = gameTime;
        }
    }
}