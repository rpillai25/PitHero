using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Salamander-like creature with glowing orange scales.</summary>
    public sealed class FireLizard : IEnemy
    {
        private int _hp;

        public string Name => MonsterTextKey.Monster_FireLizard;
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Fire;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }
        public int JPYield { get; }
        public int SPYield { get; }
        public int GoldYield { get; }
        public float JoinPercentageModifier => 0.5f;

        public FireLizard(int level = 9)
        {
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel(MonsterTextKey.Monster_FireLizard);
            Level = StatConstants.ClampLevel(level > 0 ? level : presetLevel);

            // Use BalanceConfig for stats - Balanced archetype
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
            GoldYield = BalanceConfig.CalculateMonsterGoldYield(Level);

            // Fire Lizard is Fire element: resistant to Fire, weak to Water
            var resistances = new System.Collections.Generic.Dictionary<ElementType, float>
            {
                { ElementType.Fire, 0.3f },   // 30% resistance to Fire
                { ElementType.Water, -0.3f }  // 30% weakness to Water
            };
            ElementalProps = new ElementalProperties(ElementType.Fire, resistances);
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
