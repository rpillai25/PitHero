using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;

namespace RolePlayingFramework.Skills
{
    /// <summary>
    /// Computes the effective HP restored by a healing skill for a given caster,
    /// scaling the skill's base amount with the caster's Magic stat and heal power bonus.
    /// </summary>
    public static class SkillHealCalculator
    {
        /// <summary>Effective HP restored when <paramref name="caster"/> uses the skill.</summary>
        public static int GetAmount(ISkill skill, ICombatant caster)
        {
            return BalanceConfig.CalculateSkillHealAmount(skill.HPRestoreAmount, caster.GetSkillStats().Magic, caster.HealPowerBonus);
        }

        /// <summary>
        /// Effective HP restored for a caster held as a reference-typed object.
        /// Checks for ICombatant at runtime; returns base amount if caster does not implement it.
        /// </summary>
        public static int GetAmount(ISkill skill, object caster)
        {
            if (caster is ICombatant c)
                return GetAmount(skill, c);
            return skill.HPRestoreAmount;
        }
    }
}
