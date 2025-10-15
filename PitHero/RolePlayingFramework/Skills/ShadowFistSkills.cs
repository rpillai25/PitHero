using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Shadow Fist (Monk + Thief) Skills
    
    // Passives
    public sealed class ShadowCounterPassive : BaseSkill
    {
        public ShadowCounterPassive() : base("shadowfist.shadow_counter", "Shadow Counter", SkillKind.Passive, SkillTargetType.Self, 1, 0, 120) { }
        public override void ApplyPassive(Hero hero)
        {
            // Counterattack with stealth (placeholder)
        }
    }

    public sealed class FastHandsPassive : BaseSkill
    {
        public FastHandsPassive() : base("shadowfist.fast_hands", "Fast Hands", SkillKind.Passive, SkillTargetType.Self, 2, 0, 160) { }
        public override void ApplyPassive(Hero hero)
        {
            // Bonus JP gain from traps (placeholder)
        }
    }

    // Active Skills
    public sealed class SneakPunchSkill : BaseSkill
    {
        public SneakPunchSkill() : base("shadowfist.sneak_punch", "Sneak Punch", SkillKind.Active, SkillTargetType.SingleEnemy, 2, 4, 200) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // Bonus AGI damage
            if (res.Hit) primary.TakeDamage(res.Damage + (int)(stats.Agility * 1.5f));
            return "SneakPunch";
        }
    }

    public sealed class KiCloakSkill : BaseSkill
    {
        public KiCloakSkill() : base("shadowfist.ki_cloak", "Ki Cloak", SkillKind.Active, SkillTargetType.Self, 3, 6, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Gain stealth after attack (placeholder)
            return "KiCloak";
        }
    }
}
