namespace PitHero.Combat
{
    /// <summary>
    /// Final result of a battle run by <see cref="BattleEngine"/>.
    /// </summary>
    public enum BattleOutcome
    {
        /// <summary>Battle is still in progress (engine has not finished yet).</summary>
        InProgress,

        /// <summary>All monsters were defeated; party survives.</summary>
        MonstersCleared,

        /// <summary>All allies were wiped out or left the pit before monsters were cleared.</summary>
        AlliesWipedOrGone
    }
}
