using Nez;

namespace PitHero.Services
{
    /// <summary>
    /// Global service for managing game pause state
    /// </summary>
    public class PauseService
    {
        private bool _isPaused = false;

        /// <summary>
        /// Gets or sets whether the game is currently paused
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    Debug.Log($"[PauseService] Game pause state changed to: {_isPaused}");
                }
            }
        }

        /// <summary>
        /// Pauses the game
        /// </summary>
        public void Pause()
        {
            IsPaused = true;
        }

        /// <summary>
        /// Unpauses the game
        /// </summary>
        public void Unpause()
        {
            IsPaused = false;
        }

        /// <summary>
        /// Toggles the pause state
        /// </summary>
        public void Toggle()
        {
            IsPaused = !IsPaused;
        }
    }
}