using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Spell Sniper (Wizard + Arcane Archer) Skills
    
    // Passives
    public sealed class ArcaneFocusPassive : BaseSkill
    {
        public ArcaneFocusPassive() : base("spellsniper.arcane_focus", "Arcane Focus", SkillKind.Passive, SkillTargetType.Self, 0, 180, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // +3 sight, +20% magic damage at range (placeholders)
        }
    }

    public sealed class SpellPrecisionPassive : BaseSkill
    {
        public SpellPrecisionPassive() : base("spellsniper.spell_precision", "Spell Precision", SkillKind.Passive, SkillTargetType.Self, 0, 220, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // First spell/attack in fight always crits (placeholder)
        }
    }

    // Active Skills
    public sealed class MeteorArrowSkill : BaseSkill
    {
        public MeteorArrowSkill() : base("spellsniper.meteor_arrow", "Meteor Arrow", SkillKind.Active, SkillTargetType.SingleEnemy, 7, 250, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Magic damage to primary target
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            
            // AoE splash damage to surrounding
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var splash = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (splash.Hit) enemy.TakeDamage(splash.Damage / 2);
            }
            return "MeteorArrow";
        }
    }

    public sealed class ElementalStormSkill : BaseSkill
    {
        public ElementalStormSkill() : base("spellsniper.elemental_storm", "Elemental Storm", SkillKind.Active, SkillTargetType.SurroundingEnemies, 10, 220, ElementType.Wind) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Massive elemental AoE
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage((int)(res.Damage * 2.0f));
            }
            return "ElementalStorm";
        }
    }
}
