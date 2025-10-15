using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Templar (Paladin + War Mage) Skills
    
    // Passives
    public sealed class BattleMeditationPassive : BaseSkill
    {
        public BattleMeditationPassive() : base("templar.battle_meditation", "Battle Meditation", SkillKind.Passive, SkillTargetType.Self, 1, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.APTickRegen += 2; // +2 AP/tick regen
            hero.APCostReduction += 0.1f; // 10% AP cost reduction (placeholder)
        }
    }

    public sealed class DivineWardPassive : BaseSkill
    {
        public DivineWardPassive() : base("templar.divine_ward", "Divine Ward", SkillKind.Passive, SkillTargetType.Self, 2, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.PassiveDefenseBonus += 3; // +3 defense
            // Immunity to debuffs (placeholder)
        }
    }

    // Active Skills
    public sealed class SacredBladeSkill : BaseSkill
    {
        public SacredBladeSkill() : base("templar.sacred_blade", "Sacred Blade", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 8, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Heavy physical damage
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // Holy/magic damage
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage((int)(resPhys.Damage * 1.5f));
            if (resMag.Hit) primary.TakeDamage(resMag.Damage);
            return "SacredBlade";
        }
    }

    public sealed class JudgementSkill : BaseSkill
    {
        public JudgementSkill() : base("templar.judgement", "Judgement", SkillKind.Active, SkillTargetType.SurroundingEnemies, 3, 12, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Massive AoE holy/magic damage
            for (int i = 0; i < surrounding.Count; i++)
            {
                var enemy = surrounding[i];
                var res = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (res.Hit) enemy.TakeDamage((int)(res.Damage * 2.0f)); // Double damage for holy judgement
            }
            return "Judgement";
        }
    }
}
