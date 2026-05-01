using System;

namespace PitHero.Services
{
    /// <summary>Global service for broadcasting gameplay events to UI listeners such as the EventConsolePanel.</summary>
    public class GameEventService
    {
        /// <summary>Fired whenever a game event string is emitted. Listeners should subscribe and unsubscribe cleanly.</summary>
        public event Action<string> OnEvent;

        /// <summary>Broadcasts a game event message to all registered listeners.</summary>
        public void Emit(string message)
        {
            OnEvent?.Invoke(message);
        }
    }
}
