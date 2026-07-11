using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    public sealed class SneakAttackSkill : BaseSkill
    {
        public SneakAttackSkill() : base("thief.sneak_attack", SkillTextKey.Skill_Thief_SneakAttack_Name, SkillTextKey.Skill_Thief_SneakAttack_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 3, 130, ElementType.Dark) { }
        public override string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver, IBattleContext battle)
        {
            if (primary == null) return "SneakAttack";
            var stats = caster.GetTotalStats();
            var res = ResolveHit(caster, primary, DamageKind.Physical, resolver);
            // Full AGI bonus on the first offensive action of the battle; half otherwise (or out of battle)
            bool isFirst = battle != null && battle.IsFirstOffensiveAction(caster);
            int agiBonus = isFirst ? stats.Agility : stats.Agility / 2;
            if (res.Hit) primary.TakeDamage(res.Damage + agiBonus);
            return "SneakAttack";
        }
    }

    /// <summary>
    /// Vanish — data-driven Untargetable buff (Phase 4).
    /// Grants the caster the Untargetable buff for 1 turn (max 1 stack).
    /// The buff path in <c>ApplyHealingSkillEffectsAndDisplay</c> applies GrantedBuffs automatically;
    /// no Execute override is needed.
    /// </summary>
    public sealed class VanishSkill : BaseSkill
    {
        public VanishSkill() : base("thief.vanish", SkillTextKey.Skill_Thief_Vanish_Name, SkillTextKey.Skill_Thief_Vanish_Desc, SkillKind.Active, SkillTargetType.Self, 6, 180, ElementType.Dark)
        {
            GrantedBuffs = new SkillBuff[]
            {
                // durationTurns:2 — end-of-round tick consumes one turn in the cast round,
                // so 2 = "rest of cast round + all of next round" (effective 1-round protection).
                new SkillBuff(BuffType.Untargetable, magnitude: 1, durationTurns: 2, maxStacks: 1)
            };
        }
    }

    public sealed class ShadowstepPassive : BaseSkill
    {
        public ShadowstepPassive() : base("thief.shadowstep", SkillTextKey.Skill_Thief_Shadowstep_Name, SkillTextKey.Skill_Thief_Shadowstep_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 70, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.EvasionBonus += 20; // Phase 3: plumbed into GetBattleStats evasion calculation
        }
    }

    public sealed class TrapSensePassive : BaseSkill
    {
        public TrapSensePassive() : base("thief.trap_sense", SkillTextKey.Skill_Thief_TrapSense_Name, SkillTextKey.Skill_Thief_TrapSense_Desc, SkillKind.Passive, SkillTargetType.Self, 0, 90, ElementType.Neutral) { }
        public override void ApplyPassive(ICombatant c)
        {
            c.TrapSense = true;
        }
    }
}
