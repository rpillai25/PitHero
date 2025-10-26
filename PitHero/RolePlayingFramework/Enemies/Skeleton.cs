using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Resistant to status effects.</summary>
    public sealed class Skeleton : IEnemy
    {
        private int _hp;

        public string Name => "Skeleton";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Dark;
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Skeleton(int level = 6)
        {
            // Always use the preset level for Skeletons regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Skeleton");
            Level = presetLevel;
            
            // Fixed stats: HP: 24, Attack: 10, Defense: 3, Speed: 3
            Stats = new StatBlock(strength: 10, agility: 3, vitality: 4, magic: 0);
            MaxHP = 24;
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
