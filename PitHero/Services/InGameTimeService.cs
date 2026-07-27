using Nez;
using PitHero.Services;

namespace PitHero.Services
{
    public class InGameTimeService
    {
        // 1 real second = 1 in-game minute; 60 real seconds = 1 in-game hour; 1440 real seconds = 1 full day
        private const float SecondsPerInGameHour = 60f;
        private const float SecondsPerInGameDay = 1440f;

        // Start at 6:00 AM so heroes begin active
        public const float DefaultStartAccumulatedSeconds = 6 * SecondsPerInGameHour;

        private float _accumulatedSeconds = DefaultStartAccumulatedSeconds;

        public int Hour => (int)(_accumulatedSeconds / SecondsPerInGameHour) % 24;
        public int Minute => (int)(_accumulatedSeconds % SecondsPerInGameHour);

        public bool IsNighttime => Hour >= 22 || Hour < 6;
        public bool IsActiveHours => !IsNighttime;

        public float AccumulatedSeconds => _accumulatedSeconds;

        public void SetAccumulatedTime(float seconds) => _accumulatedSeconds = seconds;

        /// <summary>Restores the default 6:00 AM start. The service is a long-lived singleton, so a new
        /// game started after quitting to title must reset it or the previous game's time carries over.</summary>
        public void ResetToDefault() => _accumulatedSeconds = DefaultStartAccumulatedSeconds;

        public void Update()
        {
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true) return;
            _accumulatedSeconds += Time.DeltaTime;
        }

        public string FormatTime()
        {
            int hour = Hour;
            int minute = Minute;
            string period = hour >= 12 ? "PM" : "AM";
            int displayHour = hour % 12;
            if (displayHour == 0) displayHour = 12;
            return $"{displayHour}:{minute:D2} {period}";
        }
    }
}
