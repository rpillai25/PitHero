using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Simple beginner enemy with low physical offense.</summary>
    public sealed class Slime : IEnemy
    {
        private int _hp;

        public string Name => "Slime";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Water;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }
        public int JPYield { get; }
        public int SPYield { get; }

        public Slime(int level = 1)
        {
            // Always use the preset level for Slimes regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Slime");
            Level = presetLevel;
            
            // Use BalanceConfig for stats
            var archetype = BalanceConfig.MonsterArchetype.Balanced;
            var strength = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength);
            var agility = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility);
            var vitality = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality);
            var magic = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic);
            
            Stats = new StatBlock(strength, agility, vitality, magic);
            MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);
            _hp = MaxHP;
            ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);
            JPYield = BalanceConfig.CalculateMonsterJPYield(Level);
            SPYield = BalanceConfig.CalculateMonsterSPYield(Level);
            
            // Slime is Water element: resistant to Water, weak to Fire
            var resistances = new System.Collections.Generic.Dictionary<ElementType, float>
            {
                { ElementType.Water, 0.3f },  // 30% resistance to Water
                { ElementType.Fire, -0.3f }   // 30% weakness to Fire
            };
            ElementalProps = new ElementalProperties(ElementType.Water, resistances);
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
