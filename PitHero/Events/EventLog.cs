using System;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.Events
{
    /// <summary>
    /// Manages the log of all events for deterministic replay
    /// </summary>
    public class EventLog
    {
        private readonly List<IEvent> _events;
        private readonly object _lock = new object();
        
        public EventLog()
        {
            _events = new List<IEvent>();
        }
        
        /// <summary>
        /// Add an event to the log
        /// </summary>
        public void LogEvent(IEvent gameEvent)
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent));
                
            lock (_lock)
            {
                _events.Add(gameEvent);
            }
        }
        
        /// <summary>
        /// Get all events in chronological order
        /// </summary>
        public IReadOnlyList<IEvent> GetAllEvents()
        {
            lock (_lock)
            {
                return _events.OrderBy(e => e.GameTime).ToList().AsReadOnly();
            }
        }
        
        /// <summary>
        /// Get events within a time range
        /// </summary>
        public IReadOnlyList<IEvent> GetEventsInTimeRange(double startTime, double endTime)
        {
            lock (_lock)
            {
                return _events
                    .Where(e => e.GameTime >= startTime && e.GameTime <= endTime)
                    .OrderBy(e => e.GameTime)
                    .ToList()
                    .AsReadOnly();
            }
        }
        
        /// <summary>
        /// Get events of a specific type
        /// </summary>
        public IReadOnlyList<T> GetEventsOfType<T>() where T : class, IEvent
        {
            lock (_lock)
            {
                return _events
                    .OfType<T>()
                    .OrderBy(e => e.GameTime)
                    .ToList()
                    .AsReadOnly();
            }
        }
        
        /// <summary>
        /// Clear all events (useful for starting a new game)
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _events.Clear();
            }
        }
        
        /// <summary>
        /// Get the number of events in the log
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _events.Count;
                }
            }
        }
    }
}