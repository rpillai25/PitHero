using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Mystic Marksman (Marksman + Spellcloak) Skills
    
    public sealed class MysticAimPassive : BaseSkill
    {
        public MysticAimPassive() : base("mysticmarksman.mystic_aim", "Mystic Aim", SkillKind.Passive, SkillTargetType.Self, 1, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // +2 sight, +10% magic damage (placeholders)
        }
    }

    public sealed class ArcaneReflexPassive : BaseSkill
    {
        public ArcaneReflexPassive() : base("mysticmarksman.arcane_reflex", "Arcane Reflex", SkillKind.Passive, SkillTargetType.Self, 2, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // First attack has stealth bonus (placeholder)
        }
    }

    public sealed class SpellShotSkill : BaseSkill
    {
        public SpellShotSkill() : base("mysticmarksman.spell_shot", "Spell Shot", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 7, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Magic AoE
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            // Splash to surrounding
            for (int i = 0; i < surrounding.Count && i < 2; i++)
            {
                var enemy = surrounding[i];
                var splash = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (splash.Hit) enemy.TakeDamage(splash.Damage / 2);
            }
            return "SpellShot";
        }
    }

    public sealed class FadeVolleySkill : BaseSkill
    {
        public FadeVolleySkill() : base("mysticmarksman.fade_volley", "Fade Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 3, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Silence and AP regen
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage);
            }
            hero.RestoreAP(5);
            return "FadeVolley";
        }
    }
}
