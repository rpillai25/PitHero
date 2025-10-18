using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Arcane Stalker (Arcane Archer + Stalker) Skills
    
    public sealed class TrackersArcanaPassive : BaseSkill
    {
        public TrackersArcanaPassive() : base("arcanestalker.trackers_arcana", "Tracker's Arcana", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // See hidden magic traps, +10% magic damage (placeholders)
        }
    }

    public sealed class QuickArcaneEscapePassive : BaseSkill
    {
        public QuickArcaneEscapePassive() : base("arcanestalker.quick_arcane_escape", "Quick Arcane Escape", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.APTickRegen += 1; // Escape battles easier, AP boost
        }
    }

    public sealed class PiercingVenomSkill : BaseSkill
    {
        public PiercingVenomSkill() : base("arcanestalker.piercing_venom", "Piercing Venom", SkillKind.Active, SkillTargetType.SingleEnemy, 7, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Poison + magic AoE
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            // Splash to surrounding
            for (int i = 0; i < surrounding.Count && i < 2; i++)
            {
                var enemy = surrounding[i];
                var splash = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (splash.Hit) enemy.TakeDamage(splash.Damage / 2);
            }
            return "PiercingVenom";
        }
    }

    public sealed class ArcaneVolleySkill : BaseSkill
    {
        public ArcaneVolleySkill() : base("arcanestalker.arcane_volley", "Arcane Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Elemental AoE with silence
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage);
            }
            return "ArcaneVolley";
        }
    }
}
