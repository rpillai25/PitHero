using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    // Active
    public sealed class SpinSlashSkill : BaseSkill
    {
        public SpinSlashSkill() : base("knight.spin_slash", SkillTextKey.Skill_Knight_SpinSlash_Name, SkillTextKey.Skill_Knight_SpinSlash_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 4, 120, ElementType.Neutral) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            // Hit primary target first
            if (primary != null)
            {
                var res = ResolveHit(caster, primary, DamageKind.Physical, resolver);
                if (res.Hit)
                {
                    int dmg = (int)(res.Damage * 0.8f);
                    if (dmg < 1) dmg = 1;
                    primary.TakeDamage(dmg);
                }
            }

            // Hit all surrounding enemies
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                if (e == null) continue;
                var res = ResolveHit(caster, e, DamageKind.Physical, resolver);
                if (res.Hit)
                {
                    int dmg = (int)(res.Damage * 0.8f);
                    if (dmg < 1) dmg = 1;
                    e.TakeDamage(dmg);
                }
            }
            return "SpinSlash";
        }
    }

    public sealed class HeavyStrikeSkill : BaseSkill
    {
        public HeavyStrikeSkill() : base("knight.heavy_strike", SkillTextKey.Skill_Knight_HeavyStrike_Name, SkillTextKey.Skill_Knight_HeavyStrike_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 180, ElementType.Neutral) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            if (primary == null) return "HeavyStrike";
            var stats = caster.GetTotalStats();
            var res = ResolveHit(caster, primary, DamageKind.Physical, resolver);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Strength);
            return "HeavyStrike";
        }
    }

    // Passives
    public sealed class LightArmorPassive : BaseSkill
    {
        public LightArmorPassive() : base("knight.light_armor", SkillTextKey.Skill_Knight_LightArmor_Name, SkillTextKey.Skill_Knight_LightArmor_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 50, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.AddExtraEquipPermission(Equipment.ItemKind.ArmorRobe); // allow robes
        }
    }

    public sealed class HeavyArmorPassive : BaseSkill
    {
        public HeavyArmorPassive() : base("knight.heavy_armor", SkillTextKey.Skill_Knight_HeavyArmor_Name, SkillTextKey.Skill_Knight_HeavyArmor_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 100, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.HeavyArmorDefenseBonus = 2; // applied to defense only when ArmorMail is equipped
        }
    }
}
