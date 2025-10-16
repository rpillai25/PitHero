using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Divine Samurai (Samurai + Divine Fist) Skills
    
    public sealed class BushidoSpiritPassive : BaseSkill
    {
        public BushidoSpiritPassive() : base("divinesamurai.bushido_spirit", "Bushido Spirit", SkillKind.Passive, SkillTargetType.Self, 1, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.EnableCounter = true; // Counterattack when undetected
        }
    }

    public sealed class EnlightenedWillPassive : BaseSkill
    {
        public EnlightenedWillPassive() : base("divinesamurai.enlightened_will", "Enlightened Will", SkillKind.Passive, SkillTargetType.Self, 2, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.APTickRegen += 1; // Resist crowd control, AP boost
        }
    }

    public sealed class SacredSlashSkill : BaseSkill
    {
        public SacredSlashSkill() : base("divinesamurai.sacred_slash", "Sacred Slash", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Crit + magic
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage((int)(resPhys.Damage * 1.5f)); // Crit bonus
            if (resMag.Hit) primary.TakeDamage(resMag.Damage);
            return "SacredSlash";
        }
    }

    public sealed class DragonAuraSkill : BaseSkill
    {
        public DragonAuraSkill() : base("divinesamurai.dragon_aura", "Dragon Aura", SkillKind.Active, SkillTargetType.SurroundingEnemies, 3, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // AP gain and debuff removal for surrounding allies (placeholder)
            hero.RestoreAP(10);
            return "DragonAura";
        }
    }
}
