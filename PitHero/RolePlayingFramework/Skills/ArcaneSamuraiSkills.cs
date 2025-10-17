using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Arcane Samurai (Samurai + Spellcloak) Skills
    
    // Passives
    public sealed class MagicBladePassive : BaseSkill
    {
        public MagicBladePassive() : base("arcanesamurai.magic_blade", "Magic Blade", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // +10% crit chance with spells (placeholder)
        }
    }

    public sealed class IronMiragePassive : BaseSkill
    {
        public IronMiragePassive() : base("arcanesamurai.iron_mirage", "Iron Mirage", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance += 0.10f; // +10% evasion
            // Resist crowd control (placeholder)
        }
    }

    // Active Skills
    public sealed class IaidoBoltSkill : BaseSkill
    {
        public IaidoBoltSkill() : base("arcanesamurai.iaido_bolt", "Iaido Bolt", SkillKind.Active, SkillTargetType.SingleEnemy, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Crit + magic damage
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage((int)(resPhys.Damage * 1.5f)); // Crit bonus
            if (resMag.Hit) primary.TakeDamage(resMag.Damage);
            return "IaidoBolt";
        }
    }

    public sealed class FadeSlashSkill : BaseSkill
    {
        public FadeSlashSkill() : base("arcanesamurai.fade_slash", "Fade Slash", SkillKind.Active, SkillTargetType.Self, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Untargetable, AP regen (placeholder)
            hero.RestoreAP(8);
            return "FadeSlash";
        }
    }
}
