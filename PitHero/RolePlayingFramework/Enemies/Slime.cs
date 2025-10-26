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
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Slime(int level = 1)
        {
            // Always use the preset level for Slimes regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Slime");
            Level = presetLevel;
            
            // Fixed stats: HP: 15, Attack: 3, Defense: 1, Speed: 2
            Stats = new StatBlock(strength: 3, agility: 2, vitality: 2, magic: 0);
            MaxHP = 15;
            _hp = MaxHP;
            ExperienceYield = 10;
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
