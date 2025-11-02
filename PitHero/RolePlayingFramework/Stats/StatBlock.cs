namespace RolePlayingFramework.Stats
{
    /// <summary>Immutable bundle of primary stats. Internally uses floats for precision, converted to ints when accessed.</summary>
    public readonly struct StatBlock
    {
        // Internal storage uses floats for fractional growth rates
        private readonly float _strength;
        private readonly float _agility;
        private readonly float _vitality;
        private readonly float _magic;

        /// <summary>Gets Strength stat, rounded to nearest integer.</summary>
        public readonly int Strength => (int)(_strength + 0.5f);
        
        /// <summary>Gets Agility stat, rounded to nearest integer.</summary>
        public readonly int Agility => (int)(_agility + 0.5f);
        
        /// <summary>Gets Vitality stat, rounded to nearest integer.</summary>
        public readonly int Vitality => (int)(_vitality + 0.5f);
        
        /// <summary>Gets Magic stat, rounded to nearest integer.</summary>
        public readonly int Magic => (int)(_magic + 0.5f);

        /// <summary>Returns a StatBlock with all zeros.</summary>
        public static readonly StatBlock Zero = new StatBlock(0, 0, 0, 0);

        public StatBlock(int strength, int agility, int vitality, int magic)
        {
            _strength = strength < 0 ? 0 : strength;
            _agility = agility < 0 ? 0 : agility;
            _vitality = vitality < 0 ? 0 : vitality;
            _magic = magic < 0 ? 0 : magic;
        }

        /// <summary>
        /// Creates a StatBlock from float values.
        /// This constructor is useful for defining fractional growth rates per level.
        /// Values are stored as floats internally and rounded when accessed.
        /// </summary>
        public StatBlock(float strength, float agility, float vitality, float magic)
        {
            _strength = strength < 0 ? 0 : strength;
            _agility = agility < 0 ? 0 : agility;
            _vitality = vitality < 0 ? 0 : vitality;
            _magic = magic < 0 ? 0 : magic;
        }

        /// <summary>Adds two StatBlocks component-wise.</summary>
        public StatBlock Add(in StatBlock other)
        {
            return new StatBlock(
                _strength + other._strength,
                _agility + other._agility,
                _vitality + other._vitality,
                _magic + other._magic
            );
        }

        /// <summary>Scales a StatBlock by a factor. Returns zero StatBlock if factor is zero or negative.</summary>
        public StatBlock Scale(float factor)
        {
            if (factor <= 0f) return new StatBlock(0, 0, 0, 0);
            return new StatBlock(
                _strength * factor,
                _agility * factor,
                _vitality * factor,
                _magic * factor
            );
        }
    }
}
