using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    public sealed class HealSkill : BaseSkill
    {
        public HealSkill() : base("priest.heal", "Heal", SkillKind.Active, SkillTargetType.Self, 2, 3) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var mult = 1f + hero.HealPowerBonus;
            hero.Heal((int)((20 + hero.GetTotalStats().Magic * 2) * mult));
            return "Heal";
        }
    }

    public sealed class DefenseUpSkill : BaseSkill
    {
        public DefenseUpSkill() : base("priest.defup", "Defense Up", SkillKind.Active, SkillTargetType.Self, 3, 4) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.PassiveDefenseBonus += 1; // temporary simple stackable buff
            return "DefenseUp";
        }
    }

    public sealed class CalmSpiritPassive : BaseSkill
    {
        public CalmSpiritPassive() : base("priest.calm_spirit", "Calm Spirit", SkillKind.Passive, SkillTargetType.Self, 1, 0) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.APTickRegen += 1; // +1 AP per turn cycle
        }
    }

    public sealed class MenderPassive : BaseSkill
    {
        public MenderPassive() : base("priest.mender", "Mender", SkillKind.Passive, SkillTargetType.Self, 2, 0) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.HealPowerBonus += 0.25f; // +25% healing
        }
    }
}
