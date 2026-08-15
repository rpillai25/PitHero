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
        /// Rolls the caster's critical hit for one attack action via the per-combatant crit
        /// shuffle bags (issue #382): the base bag advances on every attack; the quickdraw bag
        /// only applies on the caster's first offensive action when Quickdraw is learned.
        /// This is the testable seam: production code passes two <c>Nez.Random.NextFloat()</c>
        /// values (always both consumed, keeping the battle RNG stream a fixed two floats per
        /// ally attack); tests supply deterministic values.
        /// </summary>
        /// <param name="caster">The attacking combatant.</param>
        /// <param name="isFirstAction">
        /// Whether this is the caster's first offensive action this battle
        /// (from <c>IBattleContext.IsFirstOffensiveAction</c>).
        /// </param>
        /// <param name="baseRoll">A value in [0, 1) for the base-crit bag draw.</param>
        /// <param name="quickdrawRoll">A value in [0, 1) for the quickdraw bag draw.</param>
        public static bool RollCrit(ICombatant caster, bool isFirstAction, float baseRoll, float quickdrawRoll)
        {
            var crit = caster.CritBags.RollBase(baseRoll);
            if (isFirstAction && caster.FirstAttackCritChance > 0f)
                crit |= caster.CritBags.RollQuickdraw(quickdrawRoll, caster.FirstAttackCritChance);
            return crit;
        }
    }
}
