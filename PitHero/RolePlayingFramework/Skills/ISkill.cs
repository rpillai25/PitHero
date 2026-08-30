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

        /// <summary>
        /// Flat threat (percent-of-max-HP units) the caster gains when this skill is used in battle,
        /// on top of any damage/heal-derived threat. Negative means "unset" — the engine falls back
        /// to <c>GameConfig.ThreatSkillAttackDefault</c> / <c>ThreatSkillSupportDefault</c>.
        /// </summary>
        int ThreatValue { get; }

        /// <summary>
        /// True for skills the battle engine fires <b>out of turn</b> as a reaction (Knight Provoke).
        /// AI turn decisions never pick a reaction skill; a player-queued cast is honoured on the caster's turn.
        /// </summary>
        bool ReactionOnly { get; }

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
