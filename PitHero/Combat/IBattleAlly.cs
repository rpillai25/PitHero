using RolePlayingFramework.Combat;

namespace PitHero.Combat
{
    /// <summary>
    /// Abstraction over a battle participant on the ally side (hero or mercenary).
    /// Gives the engine a uniform view without touching Nez entities.
    /// </summary>
    public interface IBattleAlly
    {
        /// <summary>The combatant stat/action interface (Hero or Mercenary).</summary>
        ICombatant Combatant { get; }

        /// <summary>True when this ally represents the hero rather than a mercenary.</summary>
        bool IsHero { get; }

        /// <summary>
        /// True when the ally is still a valid battle participant.
        /// Live: entity intact and InsidePit.
        /// Virtual: combatant is alive (CurrentHP &gt; 0).
        /// </summary>
        bool IsPresent { get; }
    }
}
