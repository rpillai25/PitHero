#if DEBUG
using Nez;

namespace PitHero.Services.Analytics
{
    /// <summary>Global manager that initializes analytics and drives periodic buffer flushes. Debug builds only.</summary>
    public class AnalyticsManager : GlobalManager
    {
        public AnalyticsManager()
        {
            AnalyticsService.Initialize();
        }

        /// <summary>Accumulates unscaled frame time into the analytics flush timer.</summary>
        public override void Update()
        {
            AnalyticsService.TickFlush(Time.UnscaledDeltaTime);
        }
    }
}
#endif
