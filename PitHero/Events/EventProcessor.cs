using System;
using System.Collections.Generic;
using Nez;
using PitHero.ECS;
using PitHero.Systems;

namespace PitHero.Events
{
    /// <summary>
    /// Manages event processing and system coordination
    /// </summary>
    public class EventProcessor
    {
        private readonly EventLog _eventLog;
        private readonly WorldState _worldState;
        private readonly List<ISystem> _systems;
        
        public EventProcessor(EventLog eventLog, WorldState worldState)
        {
            _eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
            _systems = new List<ISystem>();
        }
        
        /// <summary>
        /// Register a system to process events
        /// </summary>
        public void RegisterSystem(ISystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));
                
            if (!_systems.Contains(system))
            {
                _systems.Add(system);
            }
        }
        
        /// <summary>
        /// Unregister a system
        /// </summary>
        public void UnregisterSystem(ISystem system)
        {
            _systems.Remove(system);
        }
        
        /// <summary>
        /// Process an event immediately through all systems
        /// </summary>
        public void ProcessEvent(IEvent gameEvent)
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent));
                
            // Log the event
            _eventLog.LogEvent(gameEvent);
            
            // Process through all systems
            foreach (var system in _systems)
            {
                if (system.Enabled)
                {
                    system.ProcessEvent(gameEvent, _worldState);
                }
            }
        }
        
        /// <summary>
        /// Update all systems
        /// </summary>
        public void Update(float deltaTime)
        {
            // Update game time
            _worldState.GameTime += deltaTime;
            
            // Update all systems
            foreach (var system in _systems)
            {
                if (system.Enabled)
                {
                    system.Update(_worldState, deltaTime);
                }
            }
        }
        
        /// <summary>
        /// Replay events from the event log (for replay functionality)
        /// </summary>
        public void ReplayEvents(double startTime, double endTime)
        {
            var events = _eventLog.GetEventsInTimeRange(startTime, endTime);
            
            foreach (var gameEvent in events)
            {
                // Process through all systems (but don't log again)
                foreach (var system in _systems)
                {
                    if (system.Enabled)
                    {
                        system.ProcessEvent(gameEvent, _worldState);
                    }
                }
            }
        }
        
        /// <summary>
        /// Get all registered systems
        /// </summary>
        public IReadOnlyList<ISystem> GetSystems()
        {
            return _systems.AsReadOnly();
        }
    }
}