using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Fast enemy with a chance to poison.</summary>
    public sealed class Spider : IEnemy
    {
        private int _hp;

        public string Name => "Spider";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical;
        public ElementType Element => ElementType.Earth;
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public Spider(int level = 3)
        {
            // Always use the preset level for Spiders regardless of requested level
            var presetLevel = PitHero.Config.EnemyLevelConfig.GetPresetLevel("Spider");
            Level = presetLevel;
            
            // Fixed stats: HP: 16, Attack: 6, Defense: 1, Speed: 5
            Stats = new StatBlock(strength: 6, agility: 5, vitality: 3, magic: 0);
            MaxHP = 16;
            _hp = MaxHP;
            ExperienceYield = 25;
            
            // Spider is Earth element: resistant to Earth, weak to Wind
            var resistances = new System.Collections.Generic.Dictionary<ElementType, float>
            {
                { ElementType.Earth, 0.3f },  // 30% resistance to Earth
                { ElementType.Wind, -0.3f }   // 30% weakness to Wind
            };
            ElementalProps = new ElementalProperties(ElementType.Earth, resistances);
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
