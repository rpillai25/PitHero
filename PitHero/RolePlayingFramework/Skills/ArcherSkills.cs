using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class PowerShotSkill : BaseSkill
    {
        public PowerShotSkill() : base("archer.power_shot", SkillTextKey.Skill_Archer_PowerShot_Name, SkillTextKey.Skill_Archer_PowerShot_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 4, 130, ElementType.Neutral) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            if (primary == null) return "PowerShot";
            var res = ResolveHit(caster, primary, DamageKind.Physical, resolver);
            // High damage shot: 1.5× base
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.5f));
            return "PowerShot";
        }
    }

    public sealed class VolleySkill : BaseSkill
    {
        public VolleySkill() : base("archer.volley", SkillTextKey.Skill_Archer_Volley_Name, SkillTextKey.Skill_Archer_Volley_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 200, ElementType.Neutral) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            // Hit primary target first (AoE fix: primary was previously excluded)
            if (primary != null)
            {
                var res = ResolveHit(caster, primary, DamageKind.Physical, resolver);
                if (res.Hit) primary.TakeDamage(res.Damage);
            }

            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                if (e == null) continue;
                var res = ResolveHit(caster, e, DamageKind.Physical, resolver);
                if (res.Hit) e.TakeDamage(res.Damage);
            }
            return "Volley";
        }
    }

    public sealed class EagleEyePassive : BaseSkill
    {
        public EagleEyePassive() : base("archer.eagle_eye", SkillTextKey.Skill_Archer_EagleEye_Name, SkillTextKey.Skill_Archer_EagleEye_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 70, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.SightRangeBonus += 1; // Phase 4: wired into TiledMapService.ClearFogOfWar
        }
    }

    public sealed class QuickdrawPassive : BaseSkill
    {
        public QuickdrawPassive() : base("archer.quickdraw", SkillTextKey.Skill_Archer_Quickdraw_Name, SkillTextKey.Skill_Archer_Quickdraw_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 100, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.FirstAttackCritChance += 0.5f; // Phase 4: applied in shared attack path
        }
    }
}
