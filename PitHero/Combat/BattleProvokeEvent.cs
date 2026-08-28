namespace PitHero.Combat
{
    /// <summary>
    /// Data payload for a Provoke cast (ThreatSystem.md): the Knight either reacted out of turn to
    /// protect a wounded ally, or the player queued it from the shortcut bar.
    /// Emitted via <see cref="IBattleEventSink.OnProvoke"/>.
    /// </summary>
    public readonly struct BattleProvokeEvent
    {
        /// <summary>Name of the provoking tank.</summary>
        public readonly string TankName;

        /// <summary>"hero" or "merc".</summary>
        public readonly string TankType;

        /// <summary>Name of the ally being protected, or null for a player-queued cast with nobody in danger.</summary>
        public readonly string ProtectedName;

        /// <summary>True when the engine fired it as an out-of-turn reaction; false for a queued cast.</summary>
        public readonly bool Reaction;

        /// <summary>MP actually spent (after cost reduction).</summary>
        public readonly int MpSpent;

        /// <summary>Tank's running threat after the Provoke gain.</summary>
        public readonly float ThreatTotal;

        public BattleProvokeEvent(string tankName, string tankType, string protectedName, bool reaction,
            int mpSpent, float threatTotal)
        {
            TankName = tankName;
            TankType = tankType;
            ProtectedName = protectedName;
            Reaction = reaction;
            MpSpent = mpSpent;
            ThreatTotal = threatTotal;
        }
    }
}
