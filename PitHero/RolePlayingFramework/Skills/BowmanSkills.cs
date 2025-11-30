using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    public sealed class PowerShotSkill : BaseSkill
    {
        public PowerShotSkill() : base("bowman.power_shot", "Power Shot", "A powerful arrow shot that deals 150% physical damage to a single target.", SkillKind.Active, SkillTargetType.SingleEnemy, 4, 130, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // High damage shot
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.5f));
            return "PowerShot";
        }
    }

    public sealed class VolleySkill : BaseSkill
    {
        public VolleySkill() : base("bowman.volley", "Volley", "Fire a barrage of arrows at all surrounding enemies dealing physical damage.", SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 200, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage);
            }
            return "Volley";
        }
    }

    public sealed class EagleEyePassive : BaseSkill
    {
        public EagleEyePassive() : base("bowman.eagle_eye", "Eagle Eye", "Increases sight distance to spot enemies from farther away.", SkillKind.Passive, SkillTargetType.Self, 0, 70, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement sight distance increase mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }

    public sealed class QuickdrawPassive : BaseSkill
    {
        public QuickdrawPassive() : base("bowman.quickdraw", "Quickdraw", "The first attack in combat has an increased critical hit chance.", SkillKind.Passive, SkillTargetType.Self, 0, 100, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement first attack crit mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }
}
