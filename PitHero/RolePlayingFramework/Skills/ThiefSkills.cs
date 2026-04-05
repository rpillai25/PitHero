using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class SneakAttackSkill : BaseSkill
    {
        public SneakAttackSkill() : base("thief.sneak_attack", SkillTextKey.Skill_Thief_SneakAttack_Name, SkillTextKey.Skill_Thief_SneakAttack_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 3, 130, ElementType.Dark) { }
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
        public VanishSkill() : base("thief.vanish", SkillTextKey.Skill_Thief_Vanish_Name, SkillTextKey.Skill_Thief_Vanish_Desc, SkillKind.Active, SkillTargetType.Self, 6, 180, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // TODO: Implement untargetable status for 1 turn
            // For now, this is a placeholder that can be enhanced later
            return "Vanish";
        }
    }

    public sealed class ShadowstepPassive : BaseSkill
    {
        public ShadowstepPassive() : base("thief.shadowstep", SkillTextKey.Skill_Thief_Shadowstep_Name, SkillTextKey.Skill_Thief_Shadowstep_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 70, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement evasion chance mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }

    public sealed class TrapSensePassive : BaseSkill
    {
        public TrapSensePassive() : base("thief.trap_sense", SkillTextKey.Skill_Thief_TrapSense_Name, SkillTextKey.Skill_Thief_TrapSense_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 90, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // TODO: Implement trap detection/disarm mechanic
            // For now, this is a placeholder that can be enhanced later
        }
    }
}
