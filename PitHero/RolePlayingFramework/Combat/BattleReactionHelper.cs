namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Pure static helpers for in-battle reactions (deflect and counter).
    /// Roll values are passed in by the caller so production code uses
    /// <c>Nez.Random.NextFloat()</c> while tests can supply deterministic values.
    /// </summary>
    public static class BattleReactionHelper
    {
        /// <summary>
        /// Returns true when the defender's deflect chance is active and the roll beats it.
        /// </summary>
        /// <param name="defender">The combatant being attacked.</param>
        /// <param name="roll">A value in [0, 1) supplied by the caller.</param>
        public static bool RollDeflect(ICombatant defender, float roll)
        {
            return defender.DeflectChance > 0f && roll < defender.DeflectChance;
        }

        /// <summary>
        /// Returns true when the defender has counter enabled and is still alive after
        /// taking a hit (i.e. a counter-attack should be fired back).
        /// </summary>
        /// <param name="defender">The combatant who was just hit.</param>
        public static bool ShouldCounter(ICombatant defender)
        {
            return defender.EnableCounter && defender.CurrentHP > 0;
        }

        /// <summary>
        /// Returns true when the caster's first-attack crit chance applies and the roll wins.
        /// This is the testable seam for Quickdraw: production code passes <c>Nez.Random.NextFloat()</c>;
        /// tests supply a deterministic value.
        /// </summary>
        /// <param name="caster">The attacking combatant.</param>
        /// <param name="isFirstAction">
        /// Whether this is the caster's first offensive action this battle
        /// (from <c>IBattleContext.IsFirstOffensiveAction</c>).
        /// </param>
        /// <param name="roll">A value in [0, 1) supplied by the caller.</param>
        public static bool RollFirstAttackCrit(ICombatant caster, bool isFirstAction, float roll)
        {
            return isFirstAction && caster.FirstAttackCritChance > 0f && roll < caster.FirstAttackCritChance;
        }
    }
}
