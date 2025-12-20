using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;

namespace RolePlayingFramework.Skills
{
    public sealed class SneakAttackSkill : BaseSkill
    {
        public SneakAttackSkill() : base("thief.sneak_attack", "Sneak Attack", "A stealthy strike that deals physical damage plus bonus damage based on Agility.", SkillKind.Active, SkillTargetType.SingleEnemy, 3, 130, ElementType.Dark) { }
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
        public VanishSkill() : base("thief.vanish", "Vanish", "Disappear into the shadows, becoming untargetable for a short duration.", SkillKind.Active, SkillTargetType.Self, 6, 180, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // TODO: Implement untargetable status for 1 turn
            // For now, this is a placeholder that can be enhanced later
            return "Vanish";
        }
    }

    public sealed class ShadowstepPassive : BaseSkill
    {
        public ShadowstepPassive() : base("thief.shadowstep", "Shadowstep", "Increases chance to evade incoming attacks through superior agility.", SkillKind.Passive, SkillTargetType.Self, 0, 70, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement evasion chance mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }

    public sealed class TrapSensePassive : BaseSkill
    {
        public TrapSensePassive() : base("thief.trap_sense", "Trap Sense", "Enhanced ability to detect and disarm traps in the dungeon.", SkillKind.Passive, SkillTargetType.Self, 0, 90, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement trap detection/disarm mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }
}
