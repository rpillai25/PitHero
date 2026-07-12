namespace PitHero.Combat
{
    /// <summary>
    /// Immutable payload fired by the engine after every battle buff application.
    /// Fields mirror AnalyticsService.LogBuff parameters exactly, providing
    /// live/virtual metrics-parity when forwarded to analytics or aggregated by sinks.
    /// One event is fired per granted buff actually applied; buffs skipped by the
    /// MaxStacks at-cap guard produce no event.
    /// </summary>
    public readonly struct BattleBuffEvent
    {
        /// <summary>Display name of the caster (hero or mercenary).</summary>
        public readonly string CasterName;

        /// <summary>Skill ID that granted the buff.</summary>
        public readonly string Source;

        /// <summary>Display name of the buff target.</summary>
        public readonly string TargetName;

        /// <summary>Name of the applied BuffType (e.g. "DefenseUp", "Untargetable").</summary>
        public readonly string BuffTypeName;

        /// <summary>Buff magnitude applied.</summary>
        public readonly int Magnitude;

        /// <summary>Buff duration in turns; -1 means until battle end.</summary>
        public readonly int DurationTurns;

        /// <summary>
        /// Localised display name of the buff skill for console output; falls back to
        /// <see cref="Source"/> when null. Not part of the analytics row.
        /// </summary>
        public readonly string SourceDisplayName;

        /// <summary>
        /// Short effect label matching the floating text (e.g. "DEF+1", "EVA+40").
        /// Not part of the analytics row.
        /// </summary>
        public readonly string EffectLabel;

        /// <summary>Initialises all fields.</summary>
        public BattleBuffEvent(string casterName, string source, string targetName,
            string buffTypeName, int magnitude, int durationTurns,
            string sourceDisplayName = null, string effectLabel = null)
        {
            CasterName        = casterName;
            Source            = source;
            TargetName        = targetName;
            BuffTypeName      = buffTypeName;
            Magnitude         = magnitude;
            DurationTurns     = durationTurns;
            SourceDisplayName = sourceDisplayName;
            EffectLabel       = effectLabel;
        }
    }
}
