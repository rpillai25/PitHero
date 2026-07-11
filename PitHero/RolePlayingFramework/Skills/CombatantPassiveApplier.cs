using RolePlayingFramework.Combat;
using System.Collections.Generic;

namespace RolePlayingFramework.Skills
{
    /// <summary>
    /// Centralised passive-field reset and re-application for any ICombatant.
    /// Both Hero.ApplyPassiveSkills and Mercenary.ApplyPassiveSkills delegate here so
    /// the full set of zeroed fields can never diverge between the two classes.
    /// </summary>
    public static class CombatantPassiveApplier
    {
        /// <summary>
        /// Zeroes all passive fields on <paramref name="combatant"/>, then iterates
        /// <paramref name="learnedSkills"/> and calls <see cref="ISkill.ApplyPassive"/> on
        /// each Passive-kind skill. Plain iteration — no LINQ.
        /// </summary>
        public static void ResetAndApply(ICombatant combatant, IReadOnlyDictionary<string, ISkill> learnedSkills)
        {
            // Reset phase — keep this list in sync with ICombatant passive fields
            combatant.PassiveDefenseBonus = 0;
            combatant.DeflectChance = 0f;
            combatant.EnableCounter = false;
            combatant.MPTickRegen = 0;
            combatant.HealPowerBonus = 0f;
            combatant.FireDamageBonus = 0f;
            combatant.MPCostReduction = 0f;
            combatant.EvasionBonus = 0;
            combatant.SightRangeBonus = 0;
            combatant.FirstAttackCritChance = 0f;
            combatant.HeavyArmorDefenseBonus = 0;
            combatant.TrapSense = false;

            // Apply phase — only Passive-kind skills write the fields above
            foreach (var kv in learnedSkills)
            {
                if (kv.Value.Kind == SkillKind.Passive)
                    kv.Value.ApplyPassive(combatant);
            }
        }
    }
}
