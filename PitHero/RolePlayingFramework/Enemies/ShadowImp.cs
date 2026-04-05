using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Mischievous dark creature with glowing red eyes.</summary>
    public sealed class ShadowImp : IEnemy
    {
        private int _hp;

        public string Name => MonsterTextKey.Monster_ShadowImp;
        public EnemyId EnemyId => EnemyId.ShadowImp;
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Dark;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }
        public int JPYield { get; }
        public int SPYield { get; }
        public int GoldYield { get; }
        public float JoinPercentageModifier => 0.8f;

        public ShadowImp(int level = 7)
        {
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel(EnemyId.ShadowImp);
            Level = StatConstants.ClampLevel(level > 0 ? level : presetLevel);

            // Use BalanceConfig for stats - FastFragile archetype
            var archetype = BalanceConfig.MonsterArchetype.FastFragile;
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

            // Shadow Imp is Dark element: resistant to Dark, weak to Light
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
