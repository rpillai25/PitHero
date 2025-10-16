using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Ki Ninja (Ki Shot + Ninja) Skills
    
    public sealed class KiEvasionPassive : BaseSkill
    {
        public KiEvasionPassive() : base("kininja.ki_evasion", "Ki Evasion", SkillKind.Passive, SkillTargetType.Self, 1, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance += 0.10f; // +10% evasion
            // See traps (placeholder)
        }
    }

    public sealed class ArrowDashPassive : BaseSkill
    {
        public ArrowDashPassive() : base("kininja.arrow_dash", "Arrow Dash", SkillKind.Passive, SkillTargetType.Self, 2, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // Multi-attack after movement (placeholder)
        }
    }

    public sealed class KiSlashSkill : BaseSkill
    {
        public KiSlashSkill() : base("kininja.ki_slash", "Ki Slash", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Magic + physical damage
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage(resMag.Damage / 2);
            return "KiSlash";
        }
    }

    public sealed class NinjaFlurrySkill : BaseSkill
    {
        public NinjaFlurrySkill() : base("kininja.ninja_flurry", "Ninja Flurry", SkillKind.Active, SkillTargetType.SurroundingEnemies, 3, 7, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Multi-hit AoE
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Physical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage);
            }
            return "NinjaFlurry";
        }
    }
}
