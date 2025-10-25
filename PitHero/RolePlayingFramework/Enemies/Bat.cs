using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Fast but frail enemy with low defense.</summary>
    public sealed class Bat : IEnemy
    {
        private int _hp;

        public string Name => "Bat";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Bat(int level = 1)
        {
            // Always use the preset level for Bats regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Bat");
            Level = presetLevel;
            
            // Fixed stats: HP: 12, Attack: 4, Defense: 0, Speed: 4
            // Mapping: Attack->Strength, Speed->Agility, HP based on vitality
            Stats = new StatBlock(strength: 4, agility: 4, vitality: 2, magic: 0);
            MaxHP = 12;
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
