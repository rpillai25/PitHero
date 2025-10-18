using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Shadow Paladin (Paladin + Shadowmender) Skills
    
    // Passives
    public sealed class DarkAegisPassive : BaseSkill
    {
        public DarkAegisPassive() : base("shadowpaladin.dark_aegis", "Dark Aegis", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.PassiveDefenseBonus += 2; // +2 defense when undetected (simplified)
        }
    }

    public sealed class ShadowBlessingPassive : BaseSkill
    {
        public ShadowBlessingPassive() : base("shadowpaladin.shadow_blessing", "Shadow Blessing", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // Immunity to traps/debuffs (placeholder)
        }
    }

    // Active Skills
    public sealed class SilenceStrikeSkill : BaseSkill
    {
        public SilenceStrikeSkill() : base("shadowpaladin.silence_strike", "Silence Strike", SkillKind.Active, SkillTargetType.SingleEnemy, 7, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Physical damage + silence
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            // Silence effect (placeholder)
            return "SilenceStrike";
        }
    }

    public sealed class SoulWardSkill : BaseSkill
    {
        public SoulWardSkill() : base("shadowpaladin.soul_ward", "Soul Ward", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Shield and stealth surrounding allies (placeholder - self only)
            return "SoulWard";
        }
    }
}
