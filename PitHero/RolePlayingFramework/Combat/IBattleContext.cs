using RolePlayingFramework.Enemies;

namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Battle-scoped context passed to <see cref="ISkill.Execute"/> for effects that
    /// outlast a single action (damage-over-time, first-offensive-action tracking).
    /// Null when skills are invoked outside of a battle (out-of-battle heal path never
    /// calls Execute; this parameter is reserved for future use there).
    /// </summary>
    public interface IBattleContext
    {
        /// <summary>
        /// Registers a damage-over-time effect on an enemy.
        /// If a DoT from the same source skill already targets the same enemy it is
        /// refreshed (damage/turns updated) rather than stacked.
        /// Actor name and type are resolved from <paramref name="actor"/> at registration time
        /// so DoT tick analytics rows correctly attribute the source combatant.
        /// </summary>
        /// <param name="target">The enemy to poison/burn/etc.</param>
        /// <param name="damagePerTurn">Damage applied at end of each round.</param>
        /// <param name="turns">Number of rounds the DoT lasts.</param>
        /// <param name="sourceSkillId">Skill id used for analytics and refresh logic.</param>
        /// <param name="actor">The combatant who applied the DoT (hero or merc).</param>
        void RegisterDoT(IEnemy target, int damagePerTurn, int turns, string sourceSkillId, ICombatant actor);

        /// <summary>Returns true if this is the first offensive action the combatant has taken this battle.</summary>
        bool IsFirstOffensiveAction(ICombatant c);

        /// <summary>Records that the combatant has performed an offensive action.</summary>
        void MarkActed(ICombatant c);
    }
}
