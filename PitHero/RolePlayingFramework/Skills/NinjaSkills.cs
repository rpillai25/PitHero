using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Ninja (Knight + Thief) Skills
    
    // Passives
    public sealed class EvasionMasteryPassive : BaseSkill
    {
        public EvasionMasteryPassive() : base("ninja.evasion_mastery", "Evasion Mastery", SkillKind.Passive, SkillTargetType.Self, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // +10% evasion (placeholder)
        }
    }

    public sealed class TrapMasterPassive : BaseSkill
    {
        public TrapMasterPassive() : base("ninja.trap_master", "Trap Master", SkillKind.Passive, SkillTargetType.Self, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // Immunity to traps (placeholder)
        }
    }

    // Active Skills
    public sealed class ShadowSlashSkill : BaseSkill
    {
        public ShadowSlashSkill() : base("ninja.shadow_slash", "Shadow Slash", SkillKind.Active, SkillTargetType.SingleEnemy, 4, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // Bonus damage if undetected (placeholder - just bonus AGI damage)
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Agility);
            return "ShadowSlash";
        }
    }

    public sealed class SmokeBombSkill : BaseSkill
    {
        public SmokeBombSkill() : base("ninja.smoke_bomb", "Smoke Bomb", SkillKind.Active, SkillTargetType.Self, 6, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Untargetable for 2 turns (placeholder)
            return "SmokeBomb";
        }
    }
}
