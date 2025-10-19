using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // War Mage (Knight + Mage) Skills
    
    // Passives
    public sealed class FocusedMindPassive : BaseSkill
    {
        public FocusedMindPassive() : base("warmage.focused_mind", "Focused Mind", SkillKind.Passive, SkillTargetType.Self, 0, 100) { }
        public override void ApplyPassive(Hero hero)
        {
            // +1 sight (placeholder)
            hero.MPCostReduction += 0.1f; // Lower MP cost
        }
    }

    public sealed class ArcaneDefensePassive : BaseSkill
    {
        public ArcaneDefensePassive() : base("warmage.arcane_defense", "Arcane Defense", SkillKind.Passive, SkillTargetType.Self, 0, 140) { }
        public override void ApplyPassive(Hero hero)
        {
            // +10% magic resist (placeholder)
        }
    }

    // Active Skills
    public sealed class SpellbladeSkill : BaseSkill
    {
        public SpellbladeSkill() : base("warmage.spellblade", "Spellblade", SkillKind.Active, SkillTargetType.SingleEnemy, 6, 180) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage(resMag.Damage);
            return "Spellblade";
        }
    }

    public sealed class BlitzSkill : BaseSkill
    {
        public BlitzSkill() : base("warmage.blitz", "Blitz", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var resPhys = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                var resMag = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (resPhys.Hit) e.TakeDamage((int)(resPhys.Damage * 0.7f));
                if (resMag.Hit) e.TakeDamage((int)(resMag.Damage * 0.7f));
            }
            return "Blitz";
        }
    }
}
