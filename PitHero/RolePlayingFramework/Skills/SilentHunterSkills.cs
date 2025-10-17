using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Silent Hunter (Stalker + Ninja) Skills
    
    public sealed class StealthTrackerPassive : BaseSkill
    {
        public StealthTrackerPassive() : base("silenthunter.stealth_tracker", "Stealth Tracker", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance += 0.15f; // +15% evasion when undetected
        }
    }

    public sealed class EscapeMasterPassive : BaseSkill
    {
        public EscapeMasterPassive() : base("silenthunter.escape_master", "Escape Master", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // Immune to traps, bonus JP from escapes (placeholder)
        }
    }

    public sealed class SilentSlashSkill : BaseSkill
    {
        public SilentSlashSkill() : base("silenthunter.silent_slash", "Silent Slash", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Crit and stealth bonus
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.5f));
            return "SilentSlash";
        }
    }

    public sealed class VenomEscapeSkill : BaseSkill
    {
        public VenomEscapeSkill() : base("silenthunter.venom_escape", "Venom Escape", SkillKind.Active, SkillTargetType.Self, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Untargetable for 2 turns, AP boost (placeholder)
            hero.RestoreAP(8);
            return "VenomEscape";
        }
    }
}
