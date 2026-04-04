using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class PowerShotSkill : BaseSkill
    {
        public PowerShotSkill() : base("archer.power_shot", SkillTextKey.Skill_Archer_PowerShot_Name, SkillTextKey.Skill_Archer_PowerShot_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 4, 130, ElementType.Neutral) { }
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
        public VolleySkill() : base("archer.volley", SkillTextKey.Skill_Archer_Volley_Name, SkillTextKey.Skill_Archer_Volley_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 200, ElementType.Neutral) { }
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
        public EagleEyePassive() : base("archer.eagle_eye", SkillTextKey.Skill_Archer_EagleEye_Name, SkillTextKey.Skill_Archer_EagleEye_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 70, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement sight distance increase mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }

    public sealed class QuickdrawPassive : BaseSkill
    {
        public QuickdrawPassive() : base("archer.quickdraw", SkillTextKey.Skill_Archer_Quickdraw_Name, SkillTextKey.Skill_Archer_Quickdraw_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 100, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement first attack crit mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }
}
