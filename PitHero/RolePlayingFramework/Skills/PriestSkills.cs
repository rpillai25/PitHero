using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    public sealed class HealSkill : BaseSkill
    {
        public HealSkill() : base("priest.heal", "Heal", "Restore HP based on Magic stat, enhanced by healing power bonuses.", SkillKind.Active, SkillTargetType.Self, 3, 100, ElementType.Light, battleOnly: false) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var mult = 1f + hero.HealPowerBonus;
            hero.RestoreHP((int)((20 + hero.GetTotalStats().Magic * 2) * mult));
            return "Heal";
        }
    }

    public sealed class DefenseUpSkill : BaseSkill
    {
        public DefenseUpSkill() : base("priest.defup", "Defense Up", "Temporarily increase defense by 1. Effect stacks with multiple uses.", SkillKind.Active, SkillTargetType.Self, 4, 160, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.PassiveDefenseBonus += 1; // temporary simple stackable buff
            return "DefenseUp";
        }
    }

    public sealed class CalmSpiritPassive : BaseSkill
    {
        public CalmSpiritPassive() : base("priest.calm_spirit", "Calm Spirit", "Increases MP regeneration by 1 per turn cycle.", SkillKind.Passive, SkillTargetType.Self, 0, 50, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.MPTickRegen += 1; // +1 AP per turn cycle
        }
    }

    public sealed class MenderPassive : BaseSkill
    {
        public MenderPassive() : base("priest.mender", "Mender", "Increases all healing effects by 25%.", SkillKind.Passive, SkillTargetType.Self, 0, 80, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.HealPowerBonus += 0.25f; // +25% healing
        }
    }
}
