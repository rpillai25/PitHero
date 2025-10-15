using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Wizard (Mage + Priest) Skills
    
    // Passives
    public sealed class ManaSpringPassive : BaseSkill
    {
        public ManaSpringPassive() : base("wizard.mana_spring", "Mana Spring", SkillKind.Passive, SkillTargetType.Self, 1, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.APTickRegen += 2; // +2 AP/tick regen
        }
    }

    public sealed class BlessingPassive : BaseSkill
    {
        public BlessingPassive() : base("wizard.blessing", "Blessing", SkillKind.Passive, SkillTargetType.Self, 2, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // Resist status effects (placeholder)
        }
    }

    // Active Skills
    public sealed class MeteorSkill : BaseSkill
    {
        public MeteorSkill() : base("wizard.meteor", "Meteor", SkillKind.Active, SkillTargetType.SurroundingEnemies, 2, 8, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + stats.Magic);
            }
            return "Meteor";
        }
    }

    public sealed class PurifySkill : BaseSkill
    {
        public PurifySkill() : base("wizard.purify", "Purify", SkillKind.Active, SkillTargetType.Self, 3, 4, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Remove all debuffs (placeholder)
            return "Purify";
        }
    }
}
