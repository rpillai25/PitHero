namespace PitHero.Services.AutoJob
{
    /// <summary>
    /// Asymmetric backpressure smoother for one job (issue #375). Raw pressure is sampled in
    /// workers-worth units on the sampling cadence; rising pressure is adopted instantly (a dinner
    /// rush staffs up on the next solve) while falling pressure decays through an EMA, so demand
    /// must stay low for several samples before the recommendation drops. The granted worker count
    /// additionally drains at most one worker per drain interval — the issue's "slow drain, not
    /// sudden" scale-down. All times are scaled seconds (InGameTimeService.AccumulatedSeconds),
    /// so pausing freezes the tracker. State is transient: never persisted, rebuilt within a few
    /// samples after load.
    /// </summary>
    public sealed class BackpressureTracker
    {
        private readonly float _drainIntervalSeconds;
        private float _smoothed;
        private int _granted;
        private float _lastChangeSeconds;

        /// <summary>drainIntervalSeconds: min scaled seconds between releasing successive workers.</summary>
        public BackpressureTracker(float drainIntervalSeconds = GameConfig.AutoJobScaleDownDrainIntervalSeconds)
        {
            _drainIntervalSeconds = drainIntervalSeconds;
        }

        /// <summary>Smoothed pressure in workers-worth units (instant attack, EMA decay).</summary>
        public float SmoothedPressure => _smoothed;

        /// <summary>Current drain-limited worker recommendation.</summary>
        public int GrantedWorkers => _granted;

        /// <summary>Clears smoothing and drain state. Call when the clock rewinds (save load).</summary>
        public void Reset(float nowSeconds)
        {
            _smoothed = 0f;
            _granted = 0;
            _lastChangeSeconds = nowSeconds;
        }

        /// <summary>Feeds one raw pressure sample and advances the grant toward it.</summary>
        public void Sample(float rawWorkersNeeded, float nowSeconds)
        {
            if (rawWorkersNeeded < 0f)
                rawWorkersNeeded = 0f;

            if (rawWorkersNeeded >= _smoothed)
                _smoothed = rawWorkersNeeded;
            else
                _smoothed += (rawWorkersNeeded - _smoothed) * GameConfig.AutoJobPressureDecayAlpha;

            // Scale-up keys off the RAW signal (small epsilon for float noise): the decaying EMA
            // tail must never re-grant a worker the live signal no longer justifies.
            int target = (int)System.Math.Ceiling(rawWorkersNeeded - 0.001f);
            if (target < 0)
                target = 0;

            if (target > _granted)
            {
                // Scale up instantly: backpressure means diners are waiting right now.
                _granted = target;
                _lastChangeSeconds = nowSeconds;
            }
            else if (_granted > 0 && _smoothed <= _granted - 0.5f
                && nowSeconds - _lastChangeSeconds >= _drainIntervalSeconds)
            {
                // Scale down one worker per drain interval, never more. The half-worker margin is
                // the release hysteresis: pressure hovering just under the grant keeps the crew,
                // and the EMA's asymptotic tail can't strand the final worker forever.
                _granted--;
                _lastChangeSeconds = nowSeconds;
            }
        }
    }
}
