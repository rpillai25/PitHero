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
        public string Description { get; }
        public SkillKind Kind { get; }
        public SkillTargetType TargetType { get; }
        public int MPCost { get; }
        public int JPCost { get; }

        protected BaseSkill(string id, string name, SkillKind kind, SkillTargetType targetType, int mpCost, int jpCost)
            : this(id, name, "", kind, targetType, mpCost, jpCost)
        {
        }

        protected BaseSkill(string id, string name, string description, SkillKind kind, SkillTargetType targetType, int mpCost, int jpCost)
        {
            Id = id;
            Name = name;
            Description = description;
            Kind = kind;
            TargetType = targetType;
            MPCost = mpCost;
            JPCost = jpCost;
        }

        public virtual void ApplyPassive(Hero hero) { }
        public virtual string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver) => Name;
    }
}
