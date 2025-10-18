using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Divine Fist (Priest + Monk) Skills
    
    // Passives
    public sealed class SpiritGuardPassive : BaseSkill
    {
        public SpiritGuardPassive() : base("divinefist.spirit_guard", "Spirit Guard", SkillKind.Passive, SkillTargetType.Self, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // +10% resist debuffs (placeholder)
        }
    }

    public sealed class EnlightenedPassive : BaseSkill
    {
        public EnlightenedPassive() : base("divinefist.enlightened", "Enlightened", SkillKind.Passive, SkillTargetType.Self, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.MPTickRegen += 1; // +15% MP gain approximated as +1 MP/tick
        }
    }

    // Active Skills
    public sealed class SacredStrikeSkill : BaseSkill
    {
        public SacredStrikeSkill() : base("divinefist.sacred_strike", "Sacred Strike", SkillKind.Active, SkillTargetType.SingleEnemy, 4, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage((int)(resMag.Damage * 0.5f));
            return "SacredStrike";
        }
    }

    public sealed class AuraShieldSkill : BaseSkill
    {
        public AuraShieldSkill() : base("divinefist.aura_shield", "Aura Shield", SkillKind.Active, SkillTargetType.Self, 7, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Absorb next attack (placeholder)
            hero.PassiveDefenseBonus += 5; // Temporary shield effect
            return "AuraShield";
        }
    }
}
