using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Arcane Archer (Mage + Bowman) Skills
    
    // Passives
    public sealed class SnipePassive : BaseSkill
    {
        public SnipePassive() : base("arcanearcher.snipe", "Snipe", SkillKind.Passive, SkillTargetType.Self, 1, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // +2 sight, bonus magic damage at range (placeholder)
        }
    }

    public sealed class QuickcastPassive : BaseSkill
    {
        public QuickcastPassive() : base("arcanearcher.quickcast", "Quickcast", SkillKind.Passive, SkillTargetType.Self, 2, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // First spell in fight is free (placeholder)
        }
    }

    // Active Skills
    public sealed class PiercingArrowSkill : BaseSkill
    {
        public PiercingArrowSkill() : base("arcanearcher.piercing_arrow", "Piercing Arrow", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 6, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            // Magic AoE on hit
            if (resMag.Hit)
            {
                primary.TakeDamage(resMag.Damage);
                for (int i = 0; i < surrounding.Count; i++)
                {
                    if (surrounding[i] != primary)
                    {
                        var splashRes = resolver.Resolve(stats, surrounding[i].Stats, DamageKind.Magical, hero.Level, surrounding[i].Level);
                        if (splashRes.Hit) surrounding[i].TakeDamage((int)(splashRes.Damage * 0.3f));
                    }
                }
            }
            return "PiercingArrow";
        }
    }

    public sealed class ElementalVolleySkill : BaseSkill
    {
        public ElementalVolleySkill() : base("arcanearcher.elemental_volley", "Elemental Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 3, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + stats.Magic);
            }
            return "ElementalVolley";
        }
    }
}
