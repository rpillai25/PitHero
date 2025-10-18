using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Ki Shot (Monk + Bowman) Skills
    
    // Passives
    public sealed class KiSightPassive : BaseSkill
    {
        public KiSightPassive() : base("kishot.ki_sight", "Ki Sight", SkillKind.Passive, SkillTargetType.Self, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // +1 sight, see traps (placeholder)
        }
    }

    public sealed class ArrowMeditationPassive : BaseSkill
    {
        public ArrowMeditationPassive() : base("kishot.arrow_meditation", "Arrow Meditation", SkillKind.Passive, SkillTargetType.Self, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // +5% crit after meditation (placeholder)
        }
    }

    // Active Skills
    public sealed class KiArrowSkill : BaseSkill
    {
        public KiArrowSkill() : base("kishot.ki_arrow", "Ki Arrow", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage((int)(resMag.Damage * 0.5f));
            return "KiArrow";
        }
    }

    public sealed class ArrowFlurrySkill : BaseSkill
    {
        public ArrowFlurrySkill() : base("kishot.arrow_flurry", "Arrow Flurry", SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                // Multi-hit AoE
                if (res.Hit) e.TakeDamage((int)(res.Damage * 0.8f));
            }
            return "ArrowFlurry";
        }
    }
}
