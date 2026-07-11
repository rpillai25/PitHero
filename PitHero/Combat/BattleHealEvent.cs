namespace PitHero.Combat
{
    /// <summary>
    /// Immutable payload fired by the engine after every heal application.
    /// Fields mirror AnalyticsService.LogHeal parameters exactly, providing
    /// live/virtual metrics-parity when forwarded to analytics or aggregated by sinks.
    /// </summary>
    public readonly struct BattleHealEvent
    {
        /// <summary>Display name of the healer (hero or mercenary).</summary>
        public readonly string ActorName;

        /// <summary>Skill ID or consumable name that produced the heal.</summary>
        public readonly string Source;

        /// <summary>Display name of the heal target.</summary>
        public readonly string TargetName;

        /// <summary>HP amount restored.</summary>
        public readonly int Amount;

        /// <summary>Target HP after the heal was applied.</summary>
        public readonly int HpAfter;

        /// <summary>
        /// Localised display name of the heal source for console output (skill display
        /// name or consumable name); falls back to <see cref="Source"/> when null.
        /// Not part of the analytics row (analytics uses <see cref="Source"/>).
        /// </summary>
        public readonly string SourceDisplayName;

        /// <summary>Initialises all fields.</summary>
        public BattleHealEvent(string actorName, string source, string targetName, int amount, int hpAfter,
            string sourceDisplayName = null)
        {
            ActorName  = actorName;
            Source     = source;
            TargetName = targetName;
            Amount     = amount;
            HpAfter    = hpAfter;
            SourceDisplayName = sourceDisplayName;
        }
    }
}
