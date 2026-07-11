using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class HealSkill : BaseSkill
    {
        public HealSkill() : base("priest.heal", SkillTextKey.Skill_Priest_Heal_Name, SkillTextKey.Skill_Priest_Heal_Desc, SkillKind.Active, SkillTargetType.Self, 3, 100, ElementType.Light, battleOnly: false, hpRestoreAmount: 25, mpRestoreAmount: 0) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            caster.RestoreHP(SkillHealCalculator.GetAmount(this, caster));
            return "Heal";
        }
    }

    /// <summary>
    /// DefenseUp — data-driven buff skill (Phase 3).
    /// Grants DefenseUp +1 until battle end, up to 3 stacks.
    /// The Execute override is removed; the healing/buff path in ApplyHealingSkillEffectsAndDisplay
    /// reads GrantedBuffs and applies the buff automatically.
    /// </summary>
    public sealed class DefenseUpSkill : BaseSkill
    {
        public DefenseUpSkill() : base("priest.defup", SkillTextKey.Skill_Priest_DefenseUp_Name, SkillTextKey.Skill_Priest_DefenseUp_Desc, SkillKind.Active, SkillTargetType.Self, 4, 160, ElementType.Neutral)
        {
            GrantedBuffs = new SkillBuff[]
            {
                new SkillBuff(BuffType.DefenseUp, magnitude: 1, durationTurns: -1, maxStacks: 3)
            };
        }
    }

    public sealed class CalmSpiritPassive : BaseSkill
    {
        public CalmSpiritPassive() : base("priest.calm_spirit", SkillTextKey.Skill_Priest_CalmSpirit_Name, SkillTextKey.Skill_Priest_CalmSpirit_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 50, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.MPTickRegen += 1; // +1 MP per turn cycle
        }
    }

    public sealed class MenderPassive : BaseSkill
    {
        public MenderPassive() : base("priest.mender", SkillTextKey.Skill_Priest_Mender_Name, SkillTextKey.Skill_Priest_Mender_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 80, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.HealPowerBonus += 0.25f; // +25% healing
        }
    }
}
