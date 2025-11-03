using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Dragon Fist (Mage + Monk) Skills
    
    // Passives
    public sealed class ArcaneFuryPassive : BaseSkill
    {
        public ArcaneFuryPassive() : base("dragonfist.arcane_fury", "Arcane Fury", SkillKind.Passive, SkillTargetType.Self, 0, 110, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // +20% magic damage (placeholder - using fire damage bonus as proxy)
            hero.FireDamageBonus += 0.2f;
        }
    }

    public sealed class KiBarrierPassive : BaseSkill
    {
        public KiBarrierPassive() : base("dragonfist.ki_barrier", "Ki Barrier", SkillKind.Passive, SkillTargetType.Self, 0, 140, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // +10% physical resist (placeholder)
        }
    }

    // Active Skills
    public sealed class DragonClawSkill : BaseSkill
    {
        public DragonClawSkill() : base("dragonfist.dragon_claw", "Dragon Claw", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 190, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage(resMag.Damage);
            return "DragonClaw";
        }
    }

    public sealed class EnergyBurstSkill : BaseSkill
    {
        public EnergyBurstSkill() : base("dragonfist.energy_burst", "Energy Burst", SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 210, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var resPhys = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                var resMag = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (resPhys.Hit) e.TakeDamage((int)(resPhys.Damage * 0.6f));
                if (resMag.Hit) e.TakeDamage((int)(resMag.Damage * 0.6f));
            }
            return "EnergyBurst";
        }
    }
}
