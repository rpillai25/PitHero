using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class FireSkill : BaseSkill
    {
        public FireSkill() : base("mage.fire", SkillTextKey.Skill_Mage_Fire_Name, SkillTextKey.Skill_Mage_Fire_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 3, 120, ElementType.Fire) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            if (primary == null) return "Fire";
            var stats = caster.GetSkillStats();
            var res = ResolveHit(caster, primary, DamageKind.Magical, resolver);
            if (res.Hit) primary.TakeDamage(res.Damage + (int)(stats.Magic * (1f + caster.FireDamageBonus)));
            return "Fire";
        }
    }

    public sealed class FireStormSkill : BaseSkill
    {
        public FireStormSkill() : base("mage.firestorm", SkillTextKey.Skill_Mage_Firestorm_Name, SkillTextKey.Skill_Mage_Firestorm_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 6, 200, ElementType.Fire) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            var stats = caster.GetSkillStats();

            // Hit primary target first (AoE fix: primary was previously excluded)
            if (primary != null)
            {
                var res = ResolveHit(caster, primary, DamageKind.Magical, resolver);
                if (res.Hit) primary.TakeDamage(res.Damage + (int)(stats.Magic * 0.5f * (1f + caster.FireDamageBonus)));
            }

            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                if (e == null) continue;
                var res = ResolveHit(caster, e, DamageKind.Magical, resolver);
                if (res.Hit) e.TakeDamage(res.Damage + (int)(stats.Magic * 0.5f * (1f + caster.FireDamageBonus)));
            }
            return "FireStorm";
        }
    }

    public sealed class HeartOfFirePassive : BaseSkill
    {
        public HeartOfFirePassive() : base("mage.heart_fire", SkillTextKey.Skill_Mage_HeartFire_Name, SkillTextKey.Skill_Mage_HeartFire_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 60, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.FireDamageBonus += 0.25f; // +25% fire
        }
    }

    public sealed class EconomistPassive : BaseSkill
    {
        public EconomistPassive() : base("mage.economist", SkillTextKey.Skill_Mage_Economist_Name, SkillTextKey.Skill_Mage_Economist_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 80, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.MPCostReduction += 0.15f; // -15% MP costs
        }
    }
}
