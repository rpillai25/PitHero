using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Hits hard but slow.</summary>
    /// <remarks>
    /// Future Enhancement: Consider using BalanceConfig for dynamic stat generation:
    /// - HP: BalanceConfig.CalculateMonsterHP(level, MonsterArchetype.Tank)
    /// - Stats: BalanceConfig.CalculateMonsterStat(level, MonsterArchetype.Tank, statType)
    /// - XP: BalanceConfig.CalculateMonsterExperience(level)
    /// Tank archetype would give Orcs higher HP/Vitality and lower Agility.
    /// </remarks>
    public sealed class Orc : IEnemy
    {
        private int _hp;

        public string Name => "Orc";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Fire;
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Orc(int level = 6)
        {
            // Always use the preset level for Orcs regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Orc");
            Level = presetLevel;
            
            // Fixed stats: HP: 28, Attack: 12, Defense: 4, Speed: 2
            Stats = new StatBlock(strength: 12, agility: 2, vitality: 5, magic: 0);
            MaxHP = 28;
            _hp = MaxHP;
            ExperienceYield = 50;
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
