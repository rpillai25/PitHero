using System.Collections.Generic;
using PitHero.ECS;
using PitHero.Events;

namespace PitHero.Systems
{
    /// <summary>
    /// System that handles replay functionality by processing events from the event log
    /// </summary>
    public class ReplaySystem : BaseSystem
    {
        private bool _isReplaying;
        private double _replayStartTime;
        private double _replayCurrentTime;
        private List<IEvent> _replayEvents;
        private int _currentEventIndex;
        private WorldState _replayWorldState;
        
        public bool IsReplaying => _isReplaying;
        public double ReplayProgress => _isReplaying && _replayEvents?.Count > 0 
            ? (double)_currentEventIndex / _replayEvents.Count 
            : 0.0;
        
        public ReplaySystem()
        {
            _replayEvents = new List<IEvent>();
        }
        
        protected override void OnUpdate(WorldState worldState, float deltaTime)
        {
            if (!_isReplaying)
                return;
                
            _replayCurrentTime += deltaTime;
            
            // Process events that should have occurred by now
            while (_currentEventIndex < _replayEvents.Count)
            {
                var nextEvent = _replayEvents[_currentEventIndex];
                var eventTimeInReplay = nextEvent.GameTime - _replayStartTime;
                
                if (eventTimeInReplay <= _replayCurrentTime)
                {
                    // Process this event in the replay world state
                    ProcessReplayEvent(nextEvent);
                    _currentEventIndex++;
                }
                else
                {
                    break; // Wait for the right time
                }
            }
            
            // Check if replay is complete
            if (_currentEventIndex >= _replayEvents.Count)
            {
                StopReplay();
            }
        }
        
        protected override void OnProcessEvent(IEvent gameEvent, WorldState worldState)
        {
            // ReplaySystem typically doesn't process live events
            // It processes events from the event log during replay
        }
        
        /// <summary>
        /// Start a replay from the given event log
        /// </summary>
        public void StartReplay(EventLog eventLog, double startTime, double endTime)
        {
            if (_isReplaying)
            {
                StopReplay();
            }
            
            _replayEvents = new List<IEvent>(eventLog.GetEventsInTimeRange(startTime, endTime));
            _replayStartTime = startTime;
            _replayCurrentTime = 0.0;
            _currentEventIndex = 0;
            _isReplaying = true;
            
            // Create a separate world state for replay
            _replayWorldState = new WorldState();
            _replayWorldState.GameTime = startTime;
        }
        
        /// <summary>
        /// Start a full replay from the beginning
        /// </summary>
        public void StartFullReplay(EventLog eventLog)
        {
            var allEvents = eventLog.GetAllEvents();
            if (allEvents.Count == 0)
                return;
                
            var startTime = allEvents[0].GameTime;
            var endTime = allEvents[allEvents.Count - 1].GameTime;
            
            StartReplay(eventLog, startTime, endTime);
        }
        
        /// <summary>
        /// Stop the current replay
        /// </summary>
        public void StopReplay()
        {
            _isReplaying = false;
            _replayEvents.Clear();
            _currentEventIndex = 0;
            _replayCurrentTime = 0.0;
            _replayWorldState = null;
        }
        
        /// <summary>
        /// Pause the replay
        /// </summary>
        public void PauseReplay()
        {
            Enabled = false;
        }
        
        /// <summary>
        /// Resume the replay
        /// </summary>
        public void ResumeReplay()
        {
            Enabled = true;
        }
        
        /// <summary>
        /// Get the current replay world state (for rendering the replay overlay)
        /// </summary>
        public WorldState GetReplayWorldState()
        {
            return _replayWorldState;
        }
        
        /// <summary>
        /// Skip to a specific time in the replay
        /// </summary>
        public void SeekToTime(double targetTime)
        {
            if (!_isReplaying)
                return;
                
            var relativeTime = targetTime - _replayStartTime;
            
            if (relativeTime < _replayCurrentTime)
            {
                // Need to restart and fast-forward
                _replayCurrentTime = 0.0;
                _currentEventIndex = 0;
                _replayWorldState.Clear();
                _replayWorldState.GameTime = _replayStartTime;
            }
            
            // Fast-forward to the target time
            while (_currentEventIndex < _replayEvents.Count)
            {
                var nextEvent = _replayEvents[_currentEventIndex];
                var eventTimeInReplay = nextEvent.GameTime - _replayStartTime;
                
                if (eventTimeInReplay <= relativeTime)
                {
                    ProcessReplayEvent(nextEvent);
                    _currentEventIndex++;
                }
                else
                {
                    break;
                }
            }
            
            _replayCurrentTime = relativeTime;
        }
        
        private void ProcessReplayEvent(IEvent gameEvent)
        {
            if (_replayWorldState == null)
                return;
                
            // Process the event in the replay world state
            // Note: In a full implementation, you'd want to have the same systems
            // process these events, but operating on the replay world state instead
            // of the live world state
            
            // For now, we'll just update the replay world state's game time
            _replayWorldState.GameTime = gameEvent.GameTime;
            
            // In a complete implementation, you would:
            // 1. Have instances of all systems that can operate on the replay world state
            // 2. Process events through those systems
            // 3. Update the replay world state accordingly
        }
    }
}