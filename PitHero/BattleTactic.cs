namespace PitHero
{
    /// <summary>Battle tactic that determines AI decision-making behavior during combat.</summary>
    public enum BattleTactic
    {
        /// <summary>Aggressive tactic: use strongest attacks, ignore healing, prioritize elemental weaknesses.</summary>
        Blitz,
        /// <summary>Balanced tactic: efficient MP use, heal when needed, prioritize elemental weaknesses.</summary>
        Strategic,
        /// <summary>Defensive tactic: prioritize healing and buffs, maintain 60% HP, attack when safe.</summary>
        Defensive
    }
}
