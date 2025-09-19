using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Active
    public sealed class SpinSlashSkill : BaseSkill
    {
        public SpinSlashSkill() : base("knight.spin_slash", "Spin Slash", SkillKind.Active, SkillTargetType.SurroundingEnemies, 2, 4) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage((int)(res.Damage * 0.8f));
            }
            return "SpinSlash";
        }
    }

    public sealed class HeavyStrikeSkill : BaseSkill
    {
        public HeavyStrikeSkill() : base("knight.heavy_strike", "Heavy Strike", SkillKind.Active, SkillTargetType.SingleEnemy, 3, 5) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Strength);
            return "HeavyStrike";
        }
    }

    // Passives
    public sealed class LightArmorPassive : BaseSkill
    {
        public LightArmorPassive() : base("knight.light_armor", "Light Armor", SkillKind.Passive, SkillTargetType.Self, 1, 0) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.AddExtraEquipPermission(Equipment.ItemKind.ArmorRobe); // allow robes
        }
    }

    public sealed class HeavyArmorPassive : BaseSkill
    {
        public HeavyArmorPassive() : base("knight.heavy_armor", "Heavy Armor", SkillKind.Passive, SkillTargetType.Self, 2, 0) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.PassiveDefenseBonus += 2; // flat defense bonus applied in orchestrator damage step
        }
    }
}
