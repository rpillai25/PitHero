using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>High speed and evasion.</summary>
    /// <remarks>
    /// Future Enhancement: Consider using BalanceConfig for dynamic stat generation:
    /// - HP: BalanceConfig.CalculateMonsterHP(level, MonsterArchetype.MagicUser)
    /// - Stats: BalanceConfig.CalculateMonsterStat(level, MonsterArchetype.MagicUser, statType)
    /// - XP: BalanceConfig.CalculateMonsterExperience(level)
    /// MagicUser archetype would give Wraiths higher Magic and moderate other stats.
    /// </remarks>
    public sealed class Wraith : IEnemy
    {
        private int _hp;

        public string Name => "Wraith";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Dark;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Wraith(int level = 6)
        {
            // Always use the preset level for Wraiths regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Wraith");
            Level = presetLevel;
            
            // Fixed stats: HP: 18, Attack: 9, Defense: 2, Speed: 6
            Stats = new StatBlock(strength: 9, agility: 6, vitality: 3, magic: 0);
            MaxHP = 18;
            _hp = MaxHP;
            ExperienceYield = 50;
            
            // Wraith is Dark element: resistant to Dark, weak to Light
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
