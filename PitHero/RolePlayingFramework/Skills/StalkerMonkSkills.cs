using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Stalker Monk (Stalker + Monk) Skills
    
    public sealed class FastStalkerPassive : BaseSkill
    {
        public FastStalkerPassive() : base("stalkermonk.fast_stalker", "Fast Stalker", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance += 0.10f; // +10% evasion
            // See hidden traps (placeholder)
        }
    }

    public sealed class SwiftEscapePassive : BaseSkill
    {
        public SwiftEscapePassive() : base("stalkermonk.swift_escape", "Swift Escape", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // Escape battles easier, MP boost (placeholder)
            hero.MPTickRegen += 1;
        }
    }

    public sealed class PoisonKiSkill : BaseSkill
    {
        public PoisonKiSkill() : base("stalkermonk.poison_ki", "Poison Ki", SkillKind.Active, SkillTargetType.SingleEnemy, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            // Poison + AGI damage (placeholder)
            return "PoisonKi";
        }
    }

    public sealed class SilentFlurrySkill : BaseSkill
    {
        public SilentFlurrySkill() : base("stalkermonk.silent_flurry", "Silent Flurry", SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Multi-hit AoE with silence
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Physical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage);
            }
            return "SilentFlurry";
        }
    }
}
