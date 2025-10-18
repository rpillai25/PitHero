using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    public sealed class SneakAttackSkill : BaseSkill
    {
        public SneakAttackSkill() : base("thief.sneak_attack", "Sneak Attack", SkillKind.Active, SkillTargetType.SingleEnemy, 3, 130) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // Bonus AGI damage if undetected (simplified: always apply bonus for now)
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Agility);
            return "SneakAttack";
        }
    }

    public sealed class VanishSkill : BaseSkill
    {
        public VanishSkill() : base("thief.vanish", "Vanish", SkillKind.Active, SkillTargetType.Self, 6, 180) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // TODO: Implement untargetable status for 1 turn
            // For now, this is a placeholder that can be enhanced later
            return "Vanish";
        }
    }

    public sealed class ShadowstepPassive : BaseSkill
    {
        public ShadowstepPassive() : base("thief.shadowstep", "Shadowstep", SkillKind.Passive, SkillTargetType.Self, 0, 70) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement evasion chance mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }

    public sealed class TrapSensePassive : BaseSkill
    {
        public TrapSensePassive() : base("thief.trap_sense", "Trap Sense", SkillKind.Passive, SkillTargetType.Self, 0, 90) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement trap detection/disarm mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }
}
