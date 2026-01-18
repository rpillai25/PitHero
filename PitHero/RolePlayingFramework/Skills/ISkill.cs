using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;

namespace RolePlayingFramework.Skills
{
    /// <summary>Skill (active or passive) definition.</summary>
    public interface ISkill
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        SkillKind Kind { get; }
        SkillTargetType TargetType { get; }
        int MPCost { get; }
        int JPCost { get; }

        /// <summary>Elemental type of the skill.</summary>
        ElementType Element { get; }

        /// <summary>True if this skill can only be used during battle.</summary>
        bool BattleOnly { get; }

        /// <summary>Fixed amount of HP restored by this skill (0 if skill doesn't heal).</summary>
        int HPRestoreAmount { get; }

        /// <summary>Fixed amount of MP restored by this skill (0 if skill doesn't restore MP).</summary>
        int MPRestoreAmount { get; }

        /// <summary>Applies passive modifiers at aggregation time (no side effects).</summary>
        void ApplyPassive(Hero hero);

        /// <summary>Execute active effect (stateless). Returns descriptive tag for logging.</summary>
        string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver);
    }
}
