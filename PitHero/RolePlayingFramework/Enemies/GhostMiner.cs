using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Spectral undead still searching for treasure.</summary>
    public sealed class GhostMiner : IEnemy
    {
        private int _hp;

        public string Name => "Ghost Miner";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Magical;
        public ElementType Element => ElementType.Dark;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }
        public int JPYield { get; }
        public int SPYield { get; }
        public int GoldYield { get; }
        public float JoinPercentageModifier => 0.7f;

        public GhostMiner(int level = 14)
        {
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Ghost Miner");
            Level = StatConstants.ClampLevel(level > 0 ? level : presetLevel);

            // Use BalanceConfig for stats - MagicUser archetype
            var archetype = BalanceConfig.MonsterArchetype.MagicUser;
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
            GoldYield = BalanceConfig.CalculateMonsterGoldYield(Level);

            // Ghost Miner is Dark element: resistant to Dark, weak to Light
            var resistances = new System.Collections.Generic.Dictionary<ElementType, float>
            {
                { ElementType.Dark, 0.3f },   // 30% resistance to Dark
                { ElementType.Light, -0.3f }  // 30% weakness to Light
            };
            ElementalProps = new ElementalProperties(ElementType.Dark, resistances);
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
