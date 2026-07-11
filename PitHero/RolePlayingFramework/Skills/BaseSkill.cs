using Nez;
using PitHero;
using PitHero.Services;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Skills
{
    /// <summary>Base skill with default passive/active behaviour.</summary>
    public abstract class BaseSkill : ISkill
    {
        private static readonly SkillBuff[] _emptyBuffs = new SkillBuff[0];

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

        /// <inheritdoc/>
        public IReadOnlyList<SkillBuff> GrantedBuffs { get; protected set; }

        /// <inheritdoc/>
        public bool CleansesDebuffs { get; protected set; }

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
            GrantedBuffs = _emptyBuffs;
            CleansesDebuffs = false;
        }

        /// <summary>Default implementation — passive skills override this.</summary>
        public virtual void ApplyPassive(ICombatant c) { }

        /// <summary>Default implementation — active skills override this.</summary>
        public virtual string Execute(ICombatant caster, IEnemy primary, List<IEnemy> surrounding,
            IAttackResolver resolver, IBattleContext battle) => Name;

        /// <summary>
        /// Resolves a single hit against <paramref name="target"/> using this skill's
        /// <see cref="Element"/> for elemental damage multipliers.
        /// When <paramref name="resolver"/> is <see cref="EnhancedAttackResolver"/> the
        /// elemental overload is used (Fire skill vs Water enemy → 2× damage, etc.).
        /// Otherwise falls back to the legacy stat-block overload.
        /// </summary>
        protected AttackResult ResolveHit(ICombatant caster, IEnemy target, DamageKind kind, IAttackResolver resolver)
        {
            var enhanced = resolver as EnhancedAttackResolver;
            if (enhanced != null)
            {
                var casterBattleStats = caster.GetBattleStats();
                var targetBattleStats = BattleStats.CalculateForMonster(target);
                return enhanced.Resolve(casterBattleStats, targetBattleStats, kind, Element, target.ElementalProps);
            }
            // Legacy fallback — no elemental multiplier (same behaviour as pre-Phase-2)
            StatBlock casterStats = caster.GetTotalStats();
            return resolver.Resolve(casterStats, target.Stats, kind, caster.Level, target.Level);
        }

        /// <summary>
        /// Resolves a hit against <paramref name="target"/> with the target's defense reduced
        /// by <paramref name="defensePierceFraction"/> (e.g. 0.5 = half defense).
        /// Used by armor-piercing attacks such as Piercing Arrow.
        /// </summary>
        /// <param name="defensePierceFraction">
        /// Fraction of the target's normal defense to apply (0.0 = full ignore, 1.0 = normal).
        /// </param>
        protected AttackResult ResolveHitPiercing(ICombatant caster, IEnemy target, DamageKind kind,
            IAttackResolver resolver, float defensePierceFraction)
        {
            var enhanced = resolver as EnhancedAttackResolver;
            if (enhanced != null)
            {
                var casterBattleStats = caster.GetBattleStats();
                var fullTargetStats = BattleStats.CalculateForMonster(target);
                // Build a modified target with reduced defense
                var piercedDefense = (int)(fullTargetStats.Defense * defensePierceFraction);
                var piercedStats = new BattleStats(fullTargetStats.Attack, piercedDefense, fullTargetStats.Evasion);
                return enhanced.Resolve(casterBattleStats, piercedStats, kind, Element, target.ElementalProps);
            }

            // Legacy fallback: approximate pierce by halving the defensive stats contribution.
            // Build a pierced StatBlock with Vitality and Agility halved so the legacy
            // resolver sees lower defense input, then restore Strength and Magic.
            StatBlock casterStats = caster.GetTotalStats();
            StatBlock defenderStats = target.Stats;
            var piercedDefenderStats = new StatBlock(
                defenderStats.Strength,
                (int)(defenderStats.Agility * defensePierceFraction),
                (int)(defenderStats.Vitality * defensePierceFraction),
                defenderStats.Magic);
            return resolver.Resolve(casterStats, piercedDefenderStats, kind, caster.Level, target.Level);
        }
    }
}
