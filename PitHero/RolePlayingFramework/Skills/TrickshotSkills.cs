using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Trickshot (Marksman + Stalker) Skills
    
    // Passives
    public sealed class TrackersIntuitionPassive : BaseSkill
    {
        public TrackersIntuitionPassive() : base("trickshot.trackers_intuition", "Tracker's Intuition", SkillKind.Passive, SkillTargetType.Self, 1, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // See hidden enemies/traps, bonus JP from escapes (placeholders)
        }
    }

    public sealed class ChainShotPassive : BaseSkill
    {
        public ChainShotPassive() : base("trickshot.chain_shot", "Chain Shot", SkillKind.Passive, SkillTargetType.Self, 2, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // Multi-hit attack, silence (placeholder)
        }
    }

    // Active Skills
    public sealed class VenomVolleySkill : BaseSkill
    {
        public VenomVolleySkill() : base("trickshot.venom_volley", "Venom Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 2, 10, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Ranged AoE with poison
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Physical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage);
                // Poison effect (placeholder)
            }
            return "VenomVolley";
        }
    }

    public sealed class QuietKillSkill : BaseSkill
    {
        public QuietKillSkill() : base("trickshot.quiet_kill", "Quiet Kill", SkillKind.Active, SkillTargetType.SingleEnemy, 3, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Ignores 100% defense if undetected (simplified - just high damage)
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 3.0f)); // Triple damage from stealth
            return "QuietKill";
        }
    }
}
