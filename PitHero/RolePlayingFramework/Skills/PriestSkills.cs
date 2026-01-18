using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;

namespace RolePlayingFramework.Skills
{
    public sealed class HealSkill : BaseSkill
    {
        public HealSkill() : base("priest.heal", "Heal", "Restore 50 HP.", SkillKind.Active, SkillTargetType.Self, 3, 100, ElementType.Light, battleOnly: false, hpRestoreAmount: 50, mpRestoreAmount: 0) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.RestoreHP(HPRestoreAmount);
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
