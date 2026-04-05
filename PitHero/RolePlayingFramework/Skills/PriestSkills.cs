using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class HealSkill : BaseSkill
    {
        public HealSkill() : base("priest.heal", SkillTextKey.Skill_Priest_Heal_Name, SkillTextKey.Skill_Priest_Heal_Desc, SkillKind.Active, SkillTargetType.Self, 3, 100, ElementType.Light, battleOnly: false, hpRestoreAmount: 50, mpRestoreAmount: 0) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.RestoreHP(HPRestoreAmount);
            return "Heal";
        }
    }

    public sealed class DefenseUpSkill : BaseSkill
    {
        public DefenseUpSkill() : base("priest.defup", SkillTextKey.Skill_Priest_DefenseUp_Name, SkillTextKey.Skill_Priest_DefenseUp_Desc, SkillKind.Active, SkillTargetType.Self, 4, 160, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.PassiveDefenseBonus += 1; // temporary simple stackable buff
            return "DefenseUp";
        }
    }

    public sealed class CalmSpiritPassive : BaseSkill
    {
        public CalmSpiritPassive() : base("priest.calm_spirit", SkillTextKey.Skill_Priest_CalmSpirit_Name, SkillTextKey.Skill_Priest_CalmSpirit_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 50, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.MPTickRegen += 1; // +1 AP per turn cycle
        }
    }

    public sealed class MenderPassive : BaseSkill
    {
        public MenderPassive() : base("priest.mender", SkillTextKey.Skill_Priest_Mender_Name, SkillTextKey.Skill_Priest_Mender_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 80, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.HealPowerBonus += 0.25f; // +25% healing
        }
    }
}
