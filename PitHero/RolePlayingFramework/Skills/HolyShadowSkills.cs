using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Holy Shadow (Holy Archer + Shadowmender) Skills
    
    public sealed class DivineVeilPassive : BaseSkill
    {
        public DivineVeilPassive() : base("holyshadow.divine_veil", "Divine Veil", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // +2 sight, resist debuffs while stealthed (placeholders)
        }
    }

    public sealed class LightAndDarkPassive : BaseSkill
    {
        public LightAndDarkPassive() : base("holyshadow.light_and_dark", "Light and Dark", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.HealPowerBonus += 0.15f; // Heal allies while undetected
        }
    }

    public sealed class ShadowShotSkill : BaseSkill
    {
        public ShadowShotSkill() : base("holyshadow.shadow_shot", "Shadow Shot", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            // Holy + silence damage (placeholder)
            return "ShadowShot";
        }
    }

    public sealed class SacredSilenceSkill : BaseSkill
    {
        public SacredSilenceSkill() : base("holyshadow.sacred_silence", "Sacred Silence", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Silence all enemies in area
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage(res.Damage / 2);
            }
            return "SacredSilence";
        }
    }
}
