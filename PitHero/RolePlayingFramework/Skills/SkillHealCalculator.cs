using RolePlayingFramework.Balance;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;

namespace RolePlayingFramework.Skills
{
    /// <summary>
    /// Computes the effective HP restored by a healing skill for a given caster,
    /// scaling the skill's base amount with the caster's Magic stat and heal power bonus.
    /// </summary>
    public static class SkillHealCalculator
    {
        /// <summary>Effective HP restored when the hero casts the skill.</summary>
        public static int GetAmount(ISkill skill, Hero caster)
        {
            return BalanceConfig.CalculateSkillHealAmount(skill.HPRestoreAmount, caster.GetTotalStats().Magic, caster.HealPowerBonus);
        }

        /// <summary>Effective HP restored when a mercenary casts the skill.</summary>
        public static int GetAmount(ISkill skill, Mercenary caster)
        {
            return BalanceConfig.CalculateSkillHealAmount(skill.HPRestoreAmount, caster.GetTotalStats().Magic, caster.HealPowerBonus);
        }

        /// <summary>Effective HP restored for a caster that may be a hero or a mercenary.</summary>
        public static int GetAmount(ISkill skill, object caster)
        {
            if (caster is Hero hero)
                return GetAmount(skill, hero);
            if (caster is Mercenary mercenary)
                return GetAmount(skill, mercenary);
            return skill.HPRestoreAmount;
        }
    }
}
