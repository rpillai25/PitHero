using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    /// <summary>Base skill with default passive/active behavior.</summary>
    public abstract class BaseSkill : ISkill
    {
        public string Id { get; }
        public string Name { get; }
        public SkillKind Kind { get; }
        public SkillTargetType TargetType { get; }
        public int LearnLevel { get; }
        public int APCost { get; }
        public int JPCost { get; }

        protected BaseSkill(string id, string name, SkillKind kind, SkillTargetType targetType, int learnLevel, int apCost, int jpCost)
        {
            Id = id;
            Name = name;
            Kind = kind;
            TargetType = targetType;
            LearnLevel = learnLevel;
            APCost = apCost;
            JPCost = jpCost;
        }

        public virtual void ApplyPassive(Hero hero) { }
        public virtual string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver) => Name;
    }
}
