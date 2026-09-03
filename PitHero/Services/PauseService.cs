using Nez;
using PitHero.Services.Replay;

namespace PitHero.Services
{
    /// <summary>
    /// Global service for managing game pause state. Pause is simulation state (ticks keep counting
    /// while paused and not every coroutine is pause-aware), so during a game session every change
    /// requested by UI code is routed through the <see cref="PlayerCommandService"/> and applied on a
    /// deterministic tick; outside a session (title/creation scenes, tests) the flags change directly.
    /// </summary>
    public class PauseService
    {
        private bool _isPaused = false;
        // Independent flag set while the Farm UI is open; OR'd into IsPaused so existing
        // writers (SettingsUI, Escape key, etc.) continue to operate on _isPaused only.
        private bool _farmModePause = false;
        // Last values requested through the command queue, so back-to-back toggles in one frame
        // and UI that reads the state right after setting it see the intended value.
        private bool _requestedManualPause = false;
        private bool _requestedFarmModePause = false;

        /// <summary>
        /// Gets or sets whether the game is currently paused. The getter returns true when either
        /// the manual pause flag or the farm-mode gate is active; the setter and helpers only
        /// mutate the manual flag (via the command queue during a session).
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused || _farmModePause;
            set => RequestManualPause(value);
        }

        /// <summary>
        /// True only when the manual pause flag is set (settings menu, dialogs). Excludes the
        /// farm-mode gate, so components like the camera controller can stay interactive while
        /// the player is planning crops.
        /// </summary>
        public bool IsManuallyPaused => _isPaused;

        /// <summary>Manual pause value most recently requested (may not be applied yet).</summary>
        public bool IsManualPauseRequested => _requestedManualPause;

        /// <summary>Farm-mode pause value most recently requested (may not be applied yet).</summary>
        public bool IsFarmModePauseRequested => _requestedFarmModePause;

        /// <summary>
        /// Activates or deactivates the farm-mode pause gate. While true, IsPaused returns
        /// true regardless of the manual pause flag, stopping workers and crop growth.
        /// </summary>
        public void SetFarmModePause(bool active)
        {
            _requestedFarmModePause = active;
            if (PlayerCommandService.ShouldApplyDirectly)
            {
                ApplyFarmModePause(active);
                return;
            }
            PlayerCommandService.TryEnqueue(PlayerCommand.Flag(PlayerCommandType.SetFarmModePause, active));
        }

        /// <summary>
        /// Pauses the game
        /// </summary>
        public void Pause()
        {
            RequestManualPause(true);
        }

        /// <summary>
        /// Unpauses the game
        /// </summary>
        public void Unpause()
        {
            RequestManualPause(false);
        }

        /// <summary>
        /// Toggles the manual pause flag (does not touch the farm-mode gate).
        /// </summary>
        public void Toggle()
        {
            RequestManualPause(!_requestedManualPause);
        }

        private void RequestManualPause(bool value)
        {
            _requestedManualPause = value;
            if (PlayerCommandService.ShouldApplyDirectly)
            {
                ApplyManualPause(value);
                return;
            }
            PlayerCommandService.TryEnqueue(PlayerCommand.Flag(PlayerCommandType.SetManualPause, value));
        }

        /// <summary>Applies the manual pause flag immediately. Command handlers and scene teardown only.</summary>
        public void ApplyManualPause(bool value)
        {
            _requestedManualPause = value;
            if (_isPaused != value)
            {
                _isPaused = value;
                Debug.Log($"[PauseService] Game pause state changed to: {_isPaused}");
            }
        }

        /// <summary>Applies the farm-mode gate immediately. Command handlers and scene teardown only.</summary>
        public void ApplyFarmModePause(bool value)
        {
            _requestedFarmModePause = value;
            _farmModePause = value;
        }

        /// <summary>Clears both flags immediately (scene teardown, quit to title, replay restart).</summary>
        public void ResetImmediate()
        {
            ApplyManualPause(false);
            ApplyFarmModePause(false);
        }
    }
}
