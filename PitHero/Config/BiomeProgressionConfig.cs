namespace PitHero.Config
{
    /// <summary>
    /// Biome progression rules for pit tier rollover and effective cumulative depth calculations.
    /// The pit loops back to level 1 when it would exceed MaxBiomeLevel; each loop increments
    /// the pit tier (1–99, permanent, survives hero death).
    /// </summary>
    public static class BiomeProgressionConfig
    {
        /// <summary>
        /// Maximum pit level within a single biome loop (derived from CaveBiomeConfig, not hardcoded).
        /// </summary>
        public static int MaxBiomeLevel => CaveBiomeConfig.CaveEndLevel;

        /// <summary>Maximum pit tier. Tier is permanent and never decreases.</summary>
        public const int MaxPitTier = 99;

        /// <summary>
        /// Returns the effective cumulative depth for a given displayed pit level and pit tier.
        /// Tier is clamped to [1, MaxPitTier].
        /// Example: level=1, tier=1 → 1; level=1, tier=2 → 26; level=25, tier=1 → 25; level=19, tier=5 → 119.
        /// </summary>
        public static int GetEffectiveDepth(int pitLevel, int pitTier)
        {
            int clampedTier = pitTier < 1 ? 1 : (pitTier > MaxPitTier ? MaxPitTier : pitTier);
            return (clampedTier - 1) * MaxBiomeLevel + pitLevel;
        }

        /// <summary>
        /// Returns the displayed (biome-local) pit level for a cumulative depth value.
        /// Guard: depth less than 1 returns 1.
        /// Example: depth=25 → 25 (tier 1); depth=26 → 1 (tier 2); depth=119 → 19 (tier 5).
        /// </summary>
        public static int GetDisplayedLevelForDepth(int depth)
        {
            if (depth < 1) return 1;
            return ((depth - 1) % MaxBiomeLevel) + 1;
        }

        /// <summary>
        /// Returns the pit tier for a cumulative depth value.
        /// Guard: depth less than 1 returns 1. Result is capped at MaxPitTier.
        /// Example: depth=25 → 1; depth=26 → 2; depth=119 → 5; depth=2475 → 99 (cap).
        /// </summary>
        public static int GetTierForDepth(int depth)
        {
            if (depth < 1) return 1;
            int tier = (depth - 1) / MaxBiomeLevel + 1;
            return tier > MaxPitTier ? MaxPitTier : tier;
        }
    }
}
