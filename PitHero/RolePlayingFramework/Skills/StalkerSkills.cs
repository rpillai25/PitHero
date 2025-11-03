using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Stalker (Thief + Bowman) Skills
    
    // Passives
    public sealed class HiddenTrackerPassive : BaseSkill
    {
        public HiddenTrackerPassive() : base("stalker.hidden_tracker", "Hidden Tracker", SkillKind.Passive, SkillTargetType.Self, 0, 120, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // See hidden enemies/traps (placeholder)
        }
    }

    public sealed class QuickEscapePassive : BaseSkill
    {
        public QuickEscapePassive() : base("stalker.quick_escape", "Quick Escape", SkillKind.Passive, SkillTargetType.Self, 0, 160, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // Can escape battles easier (placeholder)
        }
    }

    // Active Skills
    public sealed class PoisonArrowSkill : BaseSkill
    {
        public PoisonArrowSkill() : base("stalker.poison_arrow", "Poison Arrow", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 200, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit)
            {
                primary.TakeDamage(res.Damage);
                // Poison effect (placeholder - bonus damage)
                primary.TakeDamage(10);
            }
            return "PoisonArrow";
        }
    }

    public sealed class SilentVolleySkill : BaseSkill
    {
        public SilentVolleySkill() : base("stalker.silent_volley", "Silent Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage);
                // Silence effect (placeholder)
            }
            return "SilentVolley";
        }
    }
}
