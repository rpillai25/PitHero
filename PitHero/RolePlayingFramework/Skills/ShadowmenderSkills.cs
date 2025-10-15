using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Shadowmender (Priest + Thief) Skills
    
    // Passives
    public sealed class ShadowMendPassive : BaseSkill
    {
        public ShadowMendPassive() : base("shadowmender.shadow_mend", "Shadow Mend", SkillKind.Passive, SkillTargetType.Self, 1, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // Heals while stealthed (placeholder)
        }
    }

    public sealed class PurgeTrapPassive : BaseSkill
    {
        public PurgeTrapPassive() : base("shadowmender.purge_trap", "Purge Trap", SkillKind.Passive, SkillTargetType.Self, 2, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // Remove traps on move (placeholder)
        }
    }

    // Active Skills
    public sealed class LifeLeechSkill : BaseSkill
    {
        public LifeLeechSkill() : base("shadowmender.life_leech", "Life Leech", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 5, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit)
            {
                primary.TakeDamage(res.Damage);
                hero.RestoreHP(res.Damage / 2);
            }
            return "LifeLeech";
        }
    }

    public sealed class VeilOfSilenceSkill : BaseSkill
    {
        public VeilOfSilenceSkill() : base("shadowmender.veil_of_silence", "Veil of Silence", SkillKind.Active, SkillTargetType.Self, 3, 6, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Silence enemies in area (placeholder)
            return "VeilOfSilence";
        }
    }
}
