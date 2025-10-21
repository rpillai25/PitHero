using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Boss enemy, much stronger than others.</summary>
    public sealed class PitLord : IEnemy
    {
        private int _hp;

        public string Name => "Pit Lord";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public PitLord(int level = 10)
        {
            // Always use the preset level for Pit Lords regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Pit Lord");
            Level = presetLevel;
            
            // Fixed stats: HP: 70, Attack: 18, Defense: 7, Speed: 4
            Stats = new StatBlock(strength: 18, agility: 4, vitality: 12, magic: 0);
            MaxHP = 70;
            _hp = MaxHP;
            ExperienceYield = 200;
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
