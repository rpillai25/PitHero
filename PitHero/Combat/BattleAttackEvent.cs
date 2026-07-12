namespace PitHero.Combat
{
    /// <summary>
    /// Immutable payload fired by the engine after every attack resolution.
    /// Fields mirror AnalyticsService.LogAttack parameters exactly, providing
    /// live/virtual metrics-parity when forwarded to analytics or aggregated by sinks.
    /// </summary>
    public readonly struct BattleAttackEvent
    {
        /// <summary>Display name of the attacking combatant.</summary>
        public readonly string ActorName;

        /// <summary>Actor role for analytics rows: "hero", "merc", or "monster".</summary>
        public readonly string ActorType;

        /// <summary>Action identifier: "physical", "physical.crit", skill id, etc.</summary>
        public readonly string Action;

        /// <summary>Display name of the target combatant.</summary>
        public readonly string TargetName;

        /// <summary>Target role for analytics rows: "hero", "merc", or "monster".</summary>
        public readonly string TargetType;

        /// <summary>Damage dealt this hit.</summary>
        public readonly int Damage;

        /// <summary>Target HP before damage was applied.</summary>
        public readonly int HpBefore;

        /// <summary>Target HP after damage was applied.</summary>
        public readonly int HpAfter;

        /// <summary>True when the target died from this hit.</summary>
        public readonly bool Killed;

        /// <summary>
        /// Localised display name of the skill for skill attacks (used for the
        /// ConsoleSkillAttack line); null for physical, counter, and DoT events.
        /// Not part of the analytics row (analytics uses <see cref="Action"/>).
        /// </summary>
        public readonly string SkillName;

        /// <summary>True when the attack was dodged: Damage is 0 and HpBefore == HpAfter.</summary>
        public readonly bool Missed;

        /// <summary>Initialises all fields.</summary>
        public BattleAttackEvent(
            string actorName, string actorType, string action,
            string targetName, string targetType,
            int damage, int hpBefore, int hpAfter, bool killed,
            string skillName = null, bool missed = false)
        {
            ActorName  = actorName;
            ActorType  = actorType;
            Action     = action;
            TargetName = targetName;
            TargetType = targetType;
            Damage     = damage;
            HpBefore   = hpBefore;
            HpAfter    = hpAfter;
            Killed     = killed;
            SkillName  = skillName;
            Missed     = missed;
        }
    }
}
