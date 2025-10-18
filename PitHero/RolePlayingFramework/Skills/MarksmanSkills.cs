using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Marksman (Knight + Bowman) Skills
    
    // Passives
    public sealed class EagleReflexesPassive : BaseSkill
    {
        public EagleReflexesPassive() : base("marksman.eagle_reflexes", "Eagle Reflexes", SkillKind.Passive, SkillTargetType.Self, 0, 110) { }
        public override void ApplyPassive(Hero hero)
        {
            // +1 sight, +5% crit chance (placeholder)
        }
    }

    public sealed class SteadyAimPassive : BaseSkill
    {
        public SteadyAimPassive() : base("marksman.steady_aim", "Steady Aim", SkillKind.Passive, SkillTargetType.Self, 0, 150) { }
        public override void ApplyPassive(Hero hero)
        {
            // Bonus damage at long range (placeholder)
        }
    }

    // Active Skills
    public sealed class PowerVolleySkill : BaseSkill
    {
        public PowerVolleySkill() : base("marksman.power_volley", "Power Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 190) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage((int)(res.Damage * 1.2f));
            }
            return "PowerVolley";
        }
    }

    public sealed class ArmorPiercerSkill : BaseSkill
    {
        public ArmorPiercerSkill() : base("marksman.armor_piercer", "Armor Piercer", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 210) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // Ignores 50% defense (placeholder - just high damage)
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.5f));
            return "ArmorPiercer";
        }
    }
}
