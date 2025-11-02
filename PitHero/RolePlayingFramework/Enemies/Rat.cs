using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Slightly evasive enemy.</summary>
    public sealed class Rat : IEnemy
    {
        private int _hp;

        public string Name => "Rat";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Neutral;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Rat(int level = 1)
        {
            // Always use the preset level for Rats regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Rat");
            Level = presetLevel;
            
            // Fixed stats: HP: 13, Attack: 3, Defense: 1, Speed: 3
            Stats = new StatBlock(strength: 3, agility: 3, vitality: 2, magic: 0);
            MaxHP = 13;
            _hp = MaxHP;
            ExperienceYield = 10;
            
            // Rat is Neutral element: no special resistances
            ElementalProps = new ElementalProperties(ElementType.Neutral);
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
