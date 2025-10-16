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
        int LearnLevel { get; }
        int APCost { get; }
        int JPCost { get; }

        /// <summary>Applies passive modifiers at aggregation time (no side effects).</summary>
        void ApplyPassive(Hero hero);

        /// <summary>Execute active effect (stateless). Returns descriptive tag for logging.</summary>
        string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver);
    }
}
