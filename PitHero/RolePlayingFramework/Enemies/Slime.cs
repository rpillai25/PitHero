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
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Slime(int level = 1)
        {
            Level = level < 1 ? 1 : level;
            // Low strength, middling vitality, poor agility, no magic
            Stats = new StatBlock(strength: 2 + Level / 2, agility: 1 + Level / 4, vitality: 3 + Level / 2, magic: 0);
            MaxHP = 20 + Stats.Vitality * 6;
            _hp = MaxHP;
            ExperienceYield = 10 + Level * 2;
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
