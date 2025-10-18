using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Spellcloak (Mage + Thief) Skills
    
    // Passives
    public sealed class MiragePassive : BaseSkill
    {
        public MiragePassive() : base("spellcloak.mirage", "Mirage", SkillKind.Passive, SkillTargetType.Self, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // +15% evasion (placeholder)
        }
    }

    public sealed class ArcaneStealthPassive : BaseSkill
    {
        public ArcaneStealthPassive() : base("spellcloak.arcane_stealth", "Arcane Stealth", SkillKind.Passive, SkillTargetType.Self, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // Magic attacks don't break stealth (placeholder)
        }
    }

    // Active Skills
    public sealed class ShadowBoltSkill : BaseSkill
    {
        public ShadowBoltSkill() : base("spellcloak.shadow_bolt", "Shadow Bolt", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            // Magic + sneak damage
            if (resMag.Hit) primary.TakeDamage(resMag.Damage + stats.Agility);
            return "ShadowBolt";
        }
    }

    public sealed class FadeSkill : BaseSkill
    {
        public FadeSkill() : base("spellcloak.fade", "Fade", SkillKind.Active, SkillTargetType.Self, 6, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Untargetable for 1 turn, MP regen (placeholder)
            hero.RestoreMP(3);
            return "Fade";
        }
    }
}
