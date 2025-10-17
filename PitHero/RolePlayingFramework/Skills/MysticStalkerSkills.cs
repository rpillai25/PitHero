using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Mystic Stalker (Stalker + Spellcloak) Skills
    
    public sealed class ArcaneTrackerPassive : BaseSkill
    {
        public ArcaneTrackerPassive() : base("mysticstalker.arcane_tracker", "Arcane Tracker", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // See hidden magic traps, bonus JP from escapes (placeholder)
        }
    }

    public sealed class QuickFadePassive : BaseSkill
    {
        public QuickFadePassive() : base("mysticstalker.quick_fade", "Quick Fade", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.APTickRegen += 1; // Escape battles easier, AP regen
        }
    }

    public sealed class PoisonBoltSkill : BaseSkill
    {
        public PoisonBoltSkill() : base("mysticstalker.poison_bolt", "Poison Bolt", SkillKind.Active, SkillTargetType.SingleEnemy, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Poison + magic damage
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            return "PoisonBolt";
        }
    }

    public sealed class SilentArcanaSkill : BaseSkill
    {
        public SilentArcanaSkill() : base("mysticstalker.silent_arcana", "Silent Arcana", SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Multi-hit AoE with silence
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage);
            }
            return "SilentArcana";
        }
    }
}
