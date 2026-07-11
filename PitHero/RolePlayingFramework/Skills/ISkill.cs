using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;

namespace RolePlayingFramework.Skills
{
    /// <summary>Skill (active or passive) definition.</summary>
    public interface ISkill
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        SkillKind Kind { get; }
        SkillTargetType TargetType { get; }
        int MPCost { get; }
        int JPCost { get; }

        /// <summary>Elemental type of the skill.</summary>
        ElementType Element { get; }

        /// <summary>True if this skill can only be used during battle.</summary>
        bool BattleOnly { get; }

        /// <summary>Fixed amount of HP restored by this skill (0 if skill doesn't heal).</summary>
        int HPRestoreAmount { get; }

        /// <summary>Fixed amount of MP restored by this skill (0 if skill doesn't restore MP).</summary>
        int MPRestoreAmount { get; }

        /// <summary>
        /// Buffs this skill grants to its target when used as a healing/self skill.
        /// Empty for attack-only skills. Applied by the battle loop's healing path.
        /// </summary>
        IReadOnlyList<SkillBuff> GrantedBuffs { get; }

        /// <summary>
        /// When true the healing path removes any ally-side debuffs from the target after
        /// applying HP/MP/buffs (leave as false until a debuff system is added in a later phase).
        /// </summary>
        bool CleansesDebuffs { get; }

        /// <summary>Applies passive modifiers to the combatant at aggregation time (no side effects).</summary>
        void ApplyPassive(ICombatant c);

        /// <summary>
        /// Executes the active effect (stateless). Returns a descriptive tag for logging.
        /// <paramref name="battle"/> is null when the skill is invoked outside of a battle.
        /// </summary>
        string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding,
            IAttackResolver resolver, IBattleContext battle);
    }
}
