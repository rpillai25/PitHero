using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    public sealed class FireSkill : BaseSkill
    {
        public FireSkill() : base("mage.fire", "Fire", "Cast a fireball at a single enemy dealing magical fire damage based on Magic stat.", SkillKind.Active, SkillTargetType.SingleEnemy, 3, 120, ElementType.Fire) { }
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
        public FireStormSkill() : base("mage.firestorm", "FireStorm", "Unleash a storm of flames hitting all surrounding enemies with fire damage.", SkillKind.Active, SkillTargetType.SurroundingEnemies, 6, 200, ElementType.Fire) { }
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
        public HeartOfFirePassive() : base("mage.heart_fire", "Heart of Fire", "Increases all fire damage by 25%.", SkillKind.Passive, SkillTargetType.Self, 0, 60, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.FireDamageBonus += 0.25f; // +25% fire
        }
    }

    public sealed class EconomistPassive : BaseSkill
    {
        public EconomistPassive() : base("mage.economist", "Economist", "Reduces MP cost of all skills by 15%.", SkillKind.Passive, SkillTargetType.Self, 0, 80, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.MPCostReduction += 0.15f; // -15% MP costs
        }
    }
}
