using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Soul Guardian (Divine Fist + Shadowmender) Skills
    
    // Passives
    public sealed class SoulMendPassive : BaseSkill
    {
        public SoulMendPassive() : base("soulguardian.soul_mend", "Soul Mend", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.HealPowerBonus += 0.2f; // +20% healing when stealthed (partial implementation)
        }
    }

    public sealed class BlessingShadowsPassive : BaseSkill
    {
        public BlessingShadowsPassive() : base("soulguardian.blessing_shadows", "Blessing of Shadows", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // Immunity to silence/debuffs (placeholder)
        }
    }

    // Active Skills
    public sealed class SpiritLeechSkill : BaseSkill
    {
        public SpiritLeechSkill() : base("soulguardian.spirit_leech", "Spirit Leech", SkillKind.Active, SkillTargetType.SingleEnemy, 6, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Deal damage and heal
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit)
            {
                primary.TakeDamage(res.Damage);
                hero.RestoreHP(res.Damage / 2); // Heal for half damage dealt
            }
            // Silence effect (placeholder)
            return "SpiritLeech";
        }
    }

    public sealed class GuardianVeilSkill : BaseSkill
    {
        public GuardianVeilSkill() : base("soulguardian.guardian_veil", "Guardian Veil", SkillKind.Active, SkillTargetType.Self, 8, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Shield + MP regen (placeholder)
            hero.RestoreMP(10);
            return "GuardianVeil";
        }
    }
}
