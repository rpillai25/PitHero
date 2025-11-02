using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>High attack, low defense enemy.</summary>
    public sealed class Snake : IEnemy
    {
        private int _hp;

        public string Name => "Snake";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Earth;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Snake(int level = 3)
        {
            // Always use the preset level for Snakes regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Snake");
            Level = presetLevel;
            
            // Use BalanceConfig for stats
            var archetype = BalanceConfig.MonsterArchetype.FastFragile;
            var strength = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength);
            var agility = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility);
            var vitality = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality);
            var magic = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic);
            
            Stats = new StatBlock(strength, agility, vitality, magic);
            MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);
            _hp = MaxHP;
            ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);
            
            // Snake is Earth element: resistant to Earth, weak to Wind
            var resistances = new System.Collections.Generic.Dictionary<ElementType, float>
            {
                { ElementType.Earth, 0.3f },  // 30% resistance to Earth
                { ElementType.Wind, -0.3f }   // 30% weakness to Wind
            };
            ElementalProps = new ElementalProperties(ElementType.Earth, resistances);
        }

        /// <summary>Inflicts damage, returns true if died.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) return false;
            _hp -= amount;
            if (_hp < 0) _hp = 0;
            return _hp == 0;
        }
    }
}
