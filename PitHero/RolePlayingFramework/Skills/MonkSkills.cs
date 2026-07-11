using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class RoundhouseSkill : BaseSkill
    {
        public RoundhouseSkill() : base("monk.roundhouse", SkillTextKey.Skill_Monk_Roundhouse_Name, SkillTextKey.Skill_Monk_Roundhouse_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 4, 120, ElementType.Neutral) { }
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
            return "Roundhouse";
        }
    }

    public sealed class FlamingFistSkill : BaseSkill
    {
        public FlamingFistSkill() : base("monk.flaming_fist", SkillTextKey.Skill_Monk_FlamingFist_Name, SkillTextKey.Skill_Monk_FlamingFist_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 170, ElementType.Fire) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            if (primary == null) return "FlamingFist";
            var stats = caster.GetTotalStats();
            var res = ResolveHit(caster, primary, DamageKind.Physical, resolver);
            // Fire bonus applied to the magic-derived portion (normalization fix)
            if (res.Hit) primary.TakeDamage(res.Damage + (int)(stats.Magic * 0.5f * (1f + caster.FireDamageBonus)));
            return "FlamingFist";
        }
    }

    public sealed class CounterPassive : BaseSkill
    {
        public CounterPassive() : base("monk.counter", SkillTextKey.Skill_Monk_Counter_Name, SkillTextKey.Skill_Monk_Counter_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 70, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.EnableCounter = true;
        }
    }

    public sealed class DeflectPassive : BaseSkill
    {
        public DeflectPassive() : base("monk.deflect", SkillTextKey.Skill_Monk_Deflect_Name, SkillTextKey.Skill_Monk_Deflect_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 90, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.DeflectChance = 0.15f; // 15%
        }
    }
}
