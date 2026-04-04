using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    // Active
    public sealed class SpinSlashSkill : BaseSkill
    {
        public SpinSlashSkill() : base("knight.spin_slash", SkillTextKey.Skill_Knight_SpinSlash_Name, SkillTextKey.Skill_Knight_SpinSlash_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 4, 120, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Calculate battle stats for the hero
            var heroBattleStats = BattleStats.CalculateForHero(hero);

            // Cast to EnhancedAttackResolver to use BattleStats overload
            var enhancedResolver = resolver as EnhancedAttackResolver;
            if (enhancedResolver == null)
            {
                // Fallback to legacy method if not enhanced resolver
                var stats = hero.GetTotalStats();

                // Hit primary target first
                if (primary != null)
                {
                    var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
                    if (res.Hit)
                    {
                        int damage = (int)(res.Damage * 0.8f);
                        if (damage < 1) damage = 1;
                        primary.TakeDamage(damage);
                    }
                }

                // Hit all surrounding enemies
                for (int i = 0; i < surrounding.Count; i++)
                {
                    var e = surrounding[i];
                    if (e == null) continue;
                    var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                    if (res.Hit)
                    {
                        int damage = (int)(res.Damage * 0.8f);
                        if (damage < 1) damage = 1;
                        e.TakeDamage(damage);
                    }
                }
                return "SpinSlash";
            }

            // Hit primary target first
            if (primary != null)
            {
                var targetBattleStats = BattleStats.CalculateForMonster(primary);
                var res = enhancedResolver.Resolve(heroBattleStats, targetBattleStats, DamageKind.Physical);
                if (res.Hit)
                {
                    int damage = (int)(res.Damage * 0.8f);
                    if (damage < 1) damage = 1;
                    primary.TakeDamage(damage);
                }
            }

            // Hit all surrounding enemies
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                if (e == null) continue;

                var targetBattleStats = BattleStats.CalculateForMonster(e);
                var res = enhancedResolver.Resolve(heroBattleStats, targetBattleStats, DamageKind.Physical);
                if (res.Hit)
                {
                    int damage = (int)(res.Damage * 0.8f);
                    if (damage < 1) damage = 1;
                    e.TakeDamage(damage);
                }
            }
            return "SpinSlash";
        }
    }

    public sealed class HeavyStrikeSkill : BaseSkill
    {
        public HeavyStrikeSkill() : base("knight.heavy_strike", SkillTextKey.Skill_Knight_HeavyStrike_Name, SkillTextKey.Skill_Knight_HeavyStrike_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 180, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            if (primary == null) return "HeavyStrike";

            var stats = hero.GetTotalStats();

            // Cast to EnhancedAttackResolver to use BattleStats overload
            var enhancedResolver = resolver as EnhancedAttackResolver;
            if (enhancedResolver != null)
            {
                var heroBattleStats = BattleStats.CalculateForHero(hero);
                var targetBattleStats = BattleStats.CalculateForMonster(primary);
                var res = enhancedResolver.Resolve(heroBattleStats, targetBattleStats, DamageKind.Physical);
                if (res.Hit)
                {
                    int bonusDamage = res.Damage + stats.Strength;
                    primary.TakeDamage(bonusDamage);
                }
            }
            else
            {
                // Fallback to legacy method
                var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
                if (res.Hit)
                {
                    int bonusDamage = res.Damage + stats.Strength;
                    primary.TakeDamage(bonusDamage);
                }
            }
            return "HeavyStrike";
        }
    }

    // Passives
    public sealed class LightArmorPassive : BaseSkill
    {
        public LightArmorPassive() : base("knight.light_armor", SkillTextKey.Skill_Knight_LightArmor_Name, SkillTextKey.Skill_Knight_LightArmor_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 50, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.AddExtraEquipPermission(Equipment.ItemKind.ArmorRobe); // allow robes
        }
    }

    public sealed class HeavyArmorPassive : BaseSkill
    {
        public HeavyArmorPassive() : base("knight.heavy_armor", SkillTextKey.Skill_Knight_HeavyArmor_Name, SkillTextKey.Skill_Knight_HeavyArmor_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 100, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.PassiveDefenseBonus += 2; // flat defense bonus applied in orchestrator damage step
        }
    }
}
