namespace PitHero.Combat
{
    /// <summary>
    /// Immutable payload fired by the engine every time an ally gains threat.
    /// Fields mirror AnalyticsService.LogThreat parameters exactly.
    /// </summary>
    public readonly struct BattleThreatEvent
    {
        /// <summary>Display name of the ally who gained threat.</summary>
        public readonly string ActorName;

        /// <summary>"hero" or "merc".</summary>
        public readonly string ActorType;

        /// <summary>What generated it: "physical", a skill id, "heal", or "evasion".</summary>
        public readonly string Source;

        /// <summary>Threat added by this event (after job scaling).</summary>
        public readonly float Amount;

        /// <summary>The actor's total threat after this event.</summary>
        public readonly float Total;

        /// <summary>Initialises all fields.</summary>
        public BattleThreatEvent(string actorName, string actorType, string source, float amount, float total)
        {
            ActorName = actorName;
            ActorType = actorType;
            Source    = source;
            Amount    = amount;
            Total     = total;
        }
    }
}
