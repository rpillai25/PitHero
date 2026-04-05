using Nez;
using PitHero;
using PitHero.Services;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;

namespace RolePlayingFramework.Skills
{
    /// <summary>Base skill with default passive/active behavior.</summary>
    public abstract class BaseSkill : ISkill
    {
        private readonly string _nameKey;
        private readonly string _descKey;
        private TextService _textService;

        private TextService GetTextService()
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService;
        }

        public string Id { get; }
        public string Name => GetTextService()?.DisplayText(TextType.Skill, _nameKey) ?? _nameKey;
        public string Description => GetTextService()?.DisplayText(TextType.Skill, _descKey) ?? _descKey;
        public SkillKind Kind { get; }
        public SkillTargetType TargetType { get; }
        public int MPCost { get; }
        public int JPCost { get; }
        public ElementType Element { get; }
        public bool BattleOnly { get; }
        public int HPRestoreAmount { get; protected set; }
        public int MPRestoreAmount { get; protected set; }

        protected BaseSkill(string id, string name, SkillKind kind, SkillTargetType targetType, int mpCost, int jpCost, ElementType element = ElementType.Neutral, bool battleOnly = true)
            : this(id, name, "", kind, targetType, mpCost, jpCost, element, battleOnly, 0, 0)
        {
        }

        protected BaseSkill(string id, string name, string description, SkillKind kind, SkillTargetType targetType, int mpCost, int jpCost, ElementType element = ElementType.Neutral, bool battleOnly = true, int hpRestoreAmount = 0, int mpRestoreAmount = 0)
        {
            Id = id;
            _nameKey = name;
            _descKey = description;
            Kind = kind;
            TargetType = targetType;
            MPCost = mpCost;
            JPCost = jpCost;
            Element = element;
            BattleOnly = battleOnly;
            HPRestoreAmount = hpRestoreAmount;
            MPRestoreAmount = mpRestoreAmount;
        }

        public virtual void ApplyPassive(Hero hero) { }
        public virtual string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver) => Name;
    }
}
