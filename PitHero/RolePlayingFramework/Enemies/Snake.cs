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
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Snake(int level = 3)
        {
            // Always use the preset level for Snakes regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Snake");
            Level = presetLevel;
            
            // Fixed stats: HP: 15, Attack: 8, Defense: 0, Speed: 4
            Stats = new StatBlock(strength: 8, agility: 4, vitality: 2, magic: 0);
            MaxHP = 15;
            _hp = MaxHP;
            ExperienceYield = 25;
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
