using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Smarter enemy, sometimes dodges.</summary>
    public sealed class Goblin : IEnemy
    {
        private int _hp;

        public string Name => "Goblin";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Earth;
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Goblin(int level = 3)
        {
            // Always use the preset level for Goblins regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Goblin");
            Level = presetLevel;
            
            // Fixed stats: HP: 20, Attack: 7, Defense: 2, Speed: 3
            Stats = new StatBlock(strength: 7, agility: 3, vitality: 3, magic: 0);
            MaxHP = 20;
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
