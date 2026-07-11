namespace RolePlayingFramework.Combat
{
    /// <summary>A single active battle-scoped buff instance on a combatant.</summary>
    public struct BattleBuff
    {
        /// <summary>What kind of buff this is.</summary>
        public BuffType Type;

        /// <summary>How much the buff adds (e.g. +1 defense, +40 evasion).</summary>
        public int Magnitude;

        /// <summary>
        /// Turns remaining before this buff expires.
        /// <c>-1</c> means the buff lasts until battle end (never expires on tick).
        /// </summary>
        public int RemainingTurns;

        /// <summary>Id of the skill that applied this buff (used for stack-cap checks).</summary>
        public string SourceSkillId;

        /// <summary>Creates a new BattleBuff.</summary>
        public BattleBuff(BuffType type, int magnitude, int remainingTurns, string sourceSkillId)
        {
            Type = type;
            Magnitude = magnitude;
            RemainingTurns = remainingTurns;
            SourceSkillId = sourceSkillId;
        }
    }
}
