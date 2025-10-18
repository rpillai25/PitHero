using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Divine Cloak (Divine Fist + Spellcloak) Skills
    
    public sealed class SpiritVeilPassive : BaseSkill
    {
        public SpiritVeilPassive() : base("divinecloak.spirit_veil", "Spirit Veil", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.EnableCounter = true; // Counterattack with magic while stealthed
        }
    }

    public sealed class EnlightenedFadePassive : BaseSkill
    {
        public EnlightenedFadePassive() : base("divinecloak.enlightened_fade", "Enlightened Fade", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.APTickRegen += 1; // +15% AP gain simplified to +1 AP/tick
            // Untargetable on AP surge (placeholder)
        }
    }

    public sealed class SacredBoltSkill : BaseSkill
    {
        public SacredBoltSkill() : base("divinecloak.sacred_bolt", "Sacred Bolt", SkillKind.Active, SkillTargetType.SingleEnemy, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            // Silence effect (placeholder)
            return "SacredBolt";
        }
    }

    public sealed class AuraCloakSkill : BaseSkill
    {
        public AuraCloakSkill() : base("divinecloak.aura_cloak", "Aura Cloak", SkillKind.Active, SkillTargetType.Self, 9, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.RestoreAP(10); // AP regen + shield (placeholder)
            return "AuraCloak";
        }
    }
}
