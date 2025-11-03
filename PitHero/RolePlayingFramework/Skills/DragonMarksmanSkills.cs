using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Dragon Marksman (Dragon Fist + Marksman) Skills
    
    // Passives
    public sealed class DragonSightPassive : BaseSkill
    {
        public DragonSightPassive() : base("dragonmarksman.dragon_sight", "Dragon Sight", SkillKind.Passive, SkillTargetType.Self, 0, 180, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // +2 sight, +15% crit (placeholders)
        }
    }

    public sealed class KiVolleyPassive : BaseSkill
    {
        public KiVolleyPassive() : base("dragonmarksman.ki_volley", "Ki Volley", SkillKind.Passive, SkillTargetType.Self, 0, 220, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // Bonus damage at range (placeholder)
        }
    }

    // Active Skills
    public sealed class DragonArrowSkill : BaseSkill
    {
        public DragonArrowSkill() : base("dragonmarksman.dragon_arrow", "Dragon Arrow", SkillKind.Active, SkillTargetType.SingleEnemy, 7, 250, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // AoE physical + magic
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage(resMag.Damage);
            // AoE splash
            for (int i = 0; i < surrounding.Count && i < 3; i++)
            {
                var enemy = surrounding[i];
                var splash = resolver.Resolve(stats, enemy.Stats, DamageKind.Physical, hero.Level, enemy.Level);
                if (splash.Hit) enemy.TakeDamage(splash.Damage / 2);
            }
            return "DragonArrow";
        }
    }

    public sealed class EnergyShotSkill : BaseSkill
    {
        public EnergyShotSkill() : base("dragonmarksman.energy_shot", "Energy Shot", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Multi-hit AoE
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Physical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage((int)(res.Damage * 1.3f)); // Multi-hit bonus
            }
            return "EnergyShot";
        }
    }
}
