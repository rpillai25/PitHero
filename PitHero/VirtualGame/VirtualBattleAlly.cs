using PitHero.Combat;
using RolePlayingFramework.Combat;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of <see cref="IBattleAlly"/> that wraps any
    /// <see cref="ICombatant"/> (a live <c>Hero</c> or <c>Mercenary</c> instance) for
    /// use in headless battles driven by <see cref="BattleEngine"/>.
    ///
    /// <para>
    /// <see cref="IsPresent"/> always returns <c>true</c>.  Virtual allies are
    /// conceptually "in the pit" for the duration of the simulation; the engine's own
    /// HP-checks (e.g. <c>HasValidAlliesInPit</c>, per-participant skip rules) correctly
    /// exclude dead combatants without requiring an extra presence gate here.
    /// This mirrors the intent of <c>LiveHeroAlly.IsPresent</c>, which also deliberately
    /// does not check HP so that a hero that dies mid-round still takes their queued turn.
    /// </para>
    /// </summary>
    public sealed class VirtualBattleAlly : IBattleAlly
    {
        /// <inheritdoc/>
        public ICombatant Combatant { get; }

        /// <inheritdoc/>
        public bool IsHero { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Always <c>true</c>.  Virtual allies never "leave the pit"; the engine's HP
        /// checks act as the sole participation gate, exactly mirroring the original
        /// battle-loop semantics.
        /// </remarks>
        public bool IsPresent => true;

        /// <summary>
        /// Creates a virtual battle ally wrapping the given combatant.
        /// </summary>
        /// <param name="combatant">The hero or mercenary stats/action interface.</param>
        /// <param name="isHero">True when this ally represents the party hero.</param>
        public VirtualBattleAlly(ICombatant combatant, bool isHero)
        {
            Combatant = combatant;
            IsHero    = isHero;
        }
    }
}
