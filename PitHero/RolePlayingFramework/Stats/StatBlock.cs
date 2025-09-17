namespace RolePlayingFramework.Stats
{
    /// <summary>Immutable bundle of primary stats.</summary>
    public readonly struct StatBlock
    {
        public readonly int Strength;
        public readonly int Agility;
        public readonly int Vitality;
        public readonly int Magic;

        public StatBlock(int strength, int agility, int vitality, int magic)
        {
            Strength = strength < 0 ? 0 : strength;
            Agility = agility < 0 ? 0 : agility;
            Vitality = vitality < 0 ? 0 : vitality;
            Magic = magic < 0 ? 0 : magic;
        }

        /// <summary>Adds two StatBlocks component-wise.</summary>
        public StatBlock Add(in StatBlock other)
            => new StatBlock(Strength + other.Strength, Agility + other.Agility, Vitality + other.Vitality, Magic + other.Magic);

        /// <summary>Scales a StatBlock by a positive factor (float), rounding to nearest int.</summary>
        public StatBlock Scale(float factor)
        {
            if (factor <= 0f) return new StatBlock(0, 0, 0, 0);
            return new StatBlock(
                (int)(Strength * factor + 0.5f),
                (int)(Agility * factor + 0.5f),
                (int)(Vitality * factor + 0.5f),
                (int)(Magic * factor + 0.5f)
            );
        }
    }
}
