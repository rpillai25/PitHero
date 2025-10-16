using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Shadow Avenger (Shadow Fist + Spellcloak) Skills
    
    public sealed class StealthCounterPassive : BaseSkill
    {
        public StealthCounterPassive() : base("shadowavenger.stealth_counter", "Stealth Counter", SkillKind.Passive, SkillTargetType.Self, 1, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.EnableCounter = true; // Counterattack with stealth
        }
    }

    public sealed class ArcaneEvasionPassive : BaseSkill
    {
        public ArcaneEvasionPassive() : base("shadowavenger.arcane_evasion", "Arcane Evasion", SkillKind.Passive, SkillTargetType.Self, 2, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance += 0.15f; // +15% evasion
            // Magic attacks don't break stealth (placeholder)
        }
    }

    public sealed class SneakBoltSkill : BaseSkill
    {
        public SneakBoltSkill() : base("shadowavenger.sneak_bolt", "Sneak Bolt", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 5, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Magic + sneak damage
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            return "SneakBolt";
        }
    }

    public sealed class KiFadeSkill : BaseSkill
    {
        public KiFadeSkill() : base("shadowavenger.ki_fade", "Ki Fade", SkillKind.Active, SkillTargetType.Self, 3, 6, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Gain stealth after attack (placeholder)
            return "KiFade";
        }
    }
}
