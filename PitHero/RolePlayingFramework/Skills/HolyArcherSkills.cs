using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Holy Archer (Priest + Bowman) Skills
    
    // Passives
    public sealed class DivineVisionPassive : BaseSkill
    {
        public DivineVisionPassive() : base("holyarcher.divine_vision", "Divine Vision", SkillKind.Passive, SkillTargetType.Self, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // +2 sight, can see hidden enemies (placeholder)
        }
    }

    public sealed class BlessingArrowPassive : BaseSkill
    {
        public BlessingArrowPassive() : base("holyarcher.blessing_arrow", "Blessing Arrow", SkillKind.Passive, SkillTargetType.Self, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // Arrows heal allies in line (placeholder)
        }
    }

    // Active Skills
    public sealed class LightshotSkill : BaseSkill
    {
        public LightshotSkill() : base("holyarcher.lightshot", "Lightshot", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Magic);
            return "Lightshot";
        }
    }

    public sealed class SacredVolleySkill : BaseSkill
    {
        public SacredVolleySkill() : base("holyarcher.sacred_volley", "Sacred Volley", SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + (int)(stats.Magic * 0.5f));
            }
            return "SacredVolley";
        }
    }
}
