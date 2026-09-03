namespace PitHero.Services
{
    /// <summary>
    /// Session-relative simulation clock: one tick per fixed simulation step since MainGameScene.Begin.
    /// Simulation code that needs a timestamp (job age, spawn time, jump timing) reads
    /// <see cref="Now"/> instead of the wall-clock <c>Time.TotalTime</c>, so a replay started from a
    /// loaded save sees the same timestamps as the original session. Registered per scene; the static
    /// accessors read the current instance and return zero headlessly.
    /// </summary>
    public sealed class SimulationClock
    {
        /// <summary>The scene's clock, or null outside a game session.</summary>
        public static SimulationClock Current { get; private set; }

        /// <summary>Ticks since session start.</summary>
        public long Tick { get; private set; }

        /// <summary>Seconds since session start (Tick times the fixed step).</summary>
        public float Seconds => Tick * GameConfig.SimulationFixedStepSeconds;

        /// <summary>Seconds since session start for the current instance, or 0 when none exists.</summary>
        public static float Now => Current != null ? Current.Seconds : 0f;

        /// <summary>Current tick for the current instance, or 0 when none exists.</summary>
        public static long CurrentTick => Current != null ? Current.Tick : 0L;

        /// <summary>Creates a clock at tick 0 and makes it the current instance.</summary>
        public SimulationClock()
        {
            Current = this;
        }

        /// <summary>Advances one fixed step. Called last in MainGameScene.Update.</summary>
        public void Advance()
        {
            Tick++;
        }

        /// <summary>Jumps the clock to an absolute tick (headless tests that age jobs; replay resync).</summary>
        public void SetTick(long tick)
        {
            Tick = tick < 0 ? 0 : tick;
        }

        /// <summary>Clears the static instance when the owning scene unloads.</summary>
        public void Detach()
        {
            if (Current == this)
                Current = null;
        }
    }
}
