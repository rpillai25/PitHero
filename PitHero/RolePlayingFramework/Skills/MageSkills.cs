using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class FireSkill : BaseSkill
    {
        public FireSkill() : base("mage.fire", SkillTextKey.Skill_Mage_Fire_Name, SkillTextKey.Skill_Mage_Fire_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 3, 120, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + (int)(stats.Magic * (1f + hero.FireDamageBonus)));
            return "Fire";
        }
    }

    public sealed class FireStormSkill : BaseSkill
    {
        public FireStormSkill() : base("mage.firestorm", SkillTextKey.Skill_Mage_Firestorm_Name, SkillTextKey.Skill_Mage_Firestorm_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 6, 200, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + (int)(stats.Magic * 0.5f * (1f + hero.FireDamageBonus)));
            }
            return "FireStorm";
        }
    }

    public sealed class HeartOfFirePassive : BaseSkill
    {
        public HeartOfFirePassive() : base("mage.heart_fire", SkillTextKey.Skill_Mage_HeartFire_Name, SkillTextKey.Skill_Mage_HeartFire_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 60, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.FireDamageBonus += 0.25f; // +25% fire
        }
    }

    public sealed class EconomistPassive : BaseSkill
    {
        public EconomistPassive() : base("mage.economist", SkillTextKey.Skill_Mage_Economist_Name, SkillTextKey.Skill_Mage_Economist_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 80, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.MPCostReduction += 0.15f; // -15% MP costs
        }
    }
}
