using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Divine Archer (Divine Fist + Holy Archer) Skills
    
    public sealed class SacredSightPassive : BaseSkill
    {
        public SacredSightPassive() : base("divinearcher.sacred_sight", "Sacred Sight", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // +2 sight, holy arrows heal allies (placeholders)
        }
    }

    public sealed class AuraBlessingPassive : BaseSkill
    {
        public AuraBlessingPassive() : base("divinearcher.aura_blessing", "Aura Blessing", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.MPTickRegen += 1; // MP regen for allies in line
        }
    }

    public sealed class LightStrikeSkill : BaseSkill
    {
        public LightStrikeSkill() : base("divinearcher.light_strike", "Light Strike", SkillKind.Active, SkillTargetType.SingleEnemy, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Physical + holy damage
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage(resMag.Damage / 2);
            return "LightStrike";
        }
    }

    public sealed class HolyVolleySkill : BaseSkill
    {
        public HolyVolleySkill() : base("divinearcher.holy_volley", "Holy Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Holy AoE
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage);
            }
            return "HolyVolley";
        }
    }
}
