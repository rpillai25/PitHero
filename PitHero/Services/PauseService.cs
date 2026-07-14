using Nez;

namespace PitHero.Services
{
    /// <summary>
    /// Global service for managing game pause state
    /// </summary>
    public class PauseService
    {
        private bool _isPaused = false;
        // Independent flag set while the Farm UI is open; OR'd into IsPaused so existing
        // writers (SettingsUI, Escape key, etc.) continue to operate on _isPaused only.
        private bool _farmModePause = false;

        /// <summary>
        /// Gets or sets whether the game is currently paused. The getter returns true when either
        /// the manual pause flag or the farm-mode gate is active; the setter and helpers only
        /// mutate the manual flag.
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused || _farmModePause;
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
        /// True only when the manual pause flag is set (settings menu, dialogs). Excludes the
        /// farm-mode gate, so components like the camera controller can stay interactive while
        /// the player is planning crops.
        /// </summary>
        public bool IsManuallyPaused => _isPaused;

        /// <summary>
        /// Activates or deactivates the farm-mode pause gate. While true, IsPaused returns
        /// true regardless of the manual pause flag, stopping workers and crop growth.
        /// </summary>
        public void SetFarmModePause(bool active)
        {
            _farmModePause = active;
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
        /// Toggles the manual pause flag (does not touch the farm-mode gate).
        /// </summary>
        public void Toggle()
        {
            IsPaused = !_isPaused;
        }
    }
}