namespace RolePlayingFramework.Skills
{
    /// <summary>
    /// Declares a buff that a skill grants to its target when used.
    /// Stored in <see cref="ISkill.GrantedBuffs"/>; the battle loop converts each entry
    /// into a <see cref="RolePlayingFramework.Combat.BattleBuff"/> when the skill is applied.
    /// </summary>
    public struct SkillBuff
    {
        /// <summary>What kind of buff is granted.</summary>
        public RolePlayingFramework.Combat.BuffType Type;

        /// <summary>How much the buff adds (e.g. +1 defense, +40 evasion).</summary>
        public int Magnitude;

        /// <summary>
        /// Duration in turns; <c>-1</c> means until battle end.
        /// </summary>
        public int DurationTurns;

        /// <summary>
        /// Maximum number of stacks of this buff (from this skill) that can coexist on the target.
        /// The battle loop skips granting additional stacks once the cap is reached.
        /// </summary>
        public int MaxStacks;

        /// <summary>Creates a new SkillBuff descriptor.</summary>
        public SkillBuff(RolePlayingFramework.Combat.BuffType type, int magnitude, int durationTurns, int maxStacks)
        {
            Type = type;
            Magnitude = magnitude;
            DurationTurns = durationTurns;
            MaxStacks = maxStacks;
        }
    }
}
