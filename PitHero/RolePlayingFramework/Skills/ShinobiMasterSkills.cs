using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Shinobi Master (Samurai + Ninja) Skills
    
    // Passives
    public sealed class ShadowReflexPassive : BaseSkill
    {
        public ShadowReflexPassive() : base("shinobimaster.shadow_reflex", "Shadow Reflex", SkillKind.Passive, SkillTargetType.Self, 1, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance += 0.15f; // +15% evasion
            hero.EnableCounter = true; // Counterattack when undetected (placeholder)
        }
    }

    public sealed class IronDisciplinePassive : BaseSkill
    {
        public IronDisciplinePassive() : base("shinobimaster.iron_discipline", "Iron Discipline", SkillKind.Passive, SkillTargetType.Self, 2, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // Immunity to crowd control (placeholder)
        }
    }

    // Active Skills
    public sealed class FlashStrikeSkill : BaseSkill
    {
        public FlashStrikeSkill() : base("shinobimaster.flash_strike", "Flash Strike", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // High crit chance with stealth bonus
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.8f)); // High damage with crit + stealth
            return "FlashStrike";
        }
    }

    public sealed class MistEscapeSkill : BaseSkill
    {
        public MistEscapeSkill() : base("shinobimaster.mist_escape", "Mist Escape", SkillKind.Active, SkillTargetType.Self, 3, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Untargetable for 3 turns (placeholder)
            return "MistEscape";
        }
    }
}
