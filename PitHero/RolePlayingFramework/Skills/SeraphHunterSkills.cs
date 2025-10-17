using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Seraph Hunter (Holy Archer + Ki Shot) Skills
    
    // Passives
    public sealed class DivineArrowPassive : BaseSkill
    {
        public DivineArrowPassive() : base("seraphhunter.divine_arrow", "Divine Arrow", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // +3 sight, bonus damage vs evil (placeholders)
        }
    }

    public sealed class SeraphMeditationPassive : BaseSkill
    {
        public SeraphMeditationPassive() : base("seraphhunter.seraph_meditation", "Seraph Meditation", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // +10% crit after meditation (placeholder)
        }
    }

    // Active Skills
    public sealed class SacredFlurrySkill : BaseSkill
    {
        public SacredFlurrySkill() : base("seraphhunter.sacred_flurry", "Sacred Flurry", SkillKind.Active, SkillTargetType.SurroundingEnemies, 12, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Multi-hit holy AoE
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage((int)(res.Damage * 1.5f)); // Holy multi-hit
            }
            return "SacredFlurry";
        }
    }

    public sealed class LightBarrierSkill : BaseSkill
    {
        public LightBarrierSkill() : base("seraphhunter.light_barrier", "Light Barrier", SkillKind.Active, SkillTargetType.Self, 10, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Shield all allies (placeholder - self only for now)
            return "LightBarrier";
        }
    }
}
