using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Paladin (Knight + Priest) Skills
    
    // Passives
    public sealed class KnightsHonorPassive : BaseSkill
    {
        public KnightsHonorPassive() : base("paladin.knights_honor", "Knight's Honor", SkillKind.Passive, SkillTargetType.Self, 0, 120, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // Reduces damage taken when surrounded (placeholder)
        }
    }

    public sealed class DivineShieldPassive : BaseSkill
    {
        public DivineShieldPassive() : base("paladin.divine_shield", "Divine Shield", SkillKind.Passive, SkillTargetType.Self, 0, 160, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.PassiveDefenseBonus += 2; // +2 defense
            // Resist debuffs (placeholder)
        }
    }

    // Active Skills
    public sealed class HolyStrikeSkill : BaseSkill
    {
        public HolyStrikeSkill() : base("paladin.holy_strike", "Holy Strike", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 200, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Physical damage
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // Holy (magic) damage
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage(resMag.Damage / 2);
            return "HolyStrike";
        }
    }

    public sealed class AuraHealSkill : BaseSkill
    {
        public AuraHealSkill() : base("paladin.aura_heal", "Aura Heal", SkillKind.Active, SkillTargetType.Self, 6, 220, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var mult = 1f + hero.HealPowerBonus;
            hero.RestoreHP((int)((25 + stats.Magic * 2) * mult));
            // Debuff removal for surrounding allies (placeholder - self only for now)
            return "AuraHeal";
        }
    }
}
