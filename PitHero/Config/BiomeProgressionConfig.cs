namespace PitHero.Config
{
    /// <summary>The four pit biomes, in descent order (issue #473).</summary>
    public enum PitBiome
    {
        Cave = 0,
        Forest = 1,
        Castle = 2,
        Underworld = 3
    }

    /// <summary>
    /// Biome progression rules for the pit cycle and effective cumulative depth calculations (issue #473).
    /// A cycle is <see cref="CycleLength"/> displayed levels: every <see cref="BiomeLength"/> levels the pit
    /// moves to the next biome (Cave 1-25, Forest 26-50, Castle 51-75, Underworld 76-100). Past the last
    /// level the pit loops back to level 1 and the pit tier (1–99) increments. Hero death resets the tier
    /// to 1 along with the pit level.
    ///
    /// Level vocabulary:
    /// <list type="bullet">
    /// <item><b>Cycle level</b> — the displayed pit level (1–100), what PitWidthManager.CurrentPitLevel holds.</item>
    /// <item><b>Biome level</b> — the level within the current biome (1–25). Monster pools, boss floors,
    /// chest rarity bands and monster population are keyed on it, so every biome reuses the 1–25 content.</item>
    /// <item><b>Effective depth</b> — cumulative levels descended across tiers; enemy levels scale with it.</item>
    /// <item><b>Scaling tier</b> — one step per biome descended (depth stride <see cref="BiomeLength"/>);
    /// drives the tier-scaled "+N" gear and chest gold, so loot keeps the per-25-level curve it had when
    /// each 25-level loop was its own tier.</item>
    /// </list>
    /// </summary>
    public static class BiomeProgressionConfig
    {
        /// <summary>Levels per biome (derived from CaveBiomeConfig, not hardcoded).</summary>
        public static int BiomeLength => CaveBiomeConfig.CaveEndLevel;

        /// <summary>Number of biomes in one cycle.</summary>
        public const int BiomeCount = 4;

        /// <summary>Maximum displayed pit level within a cycle (Cave → Underworld); the tier increments past it.</summary>
        public static int CycleLength => BiomeLength * BiomeCount;

        /// <summary>Maximum pit tier. Tier only decreases when hero death resets the cycle.</summary>
        public const int MaxPitTier = 99;

        /// <summary>Highest scaling tier (gear "+N" suffix and chest gold keep the historical 1–99 range).</summary>
        public const int MaxScalingTier = 99;

        /// <summary>
        /// Returns the effective cumulative depth for a given displayed (cycle) pit level and pit tier.
        /// Tier is clamped to [1, MaxPitTier].
        /// Example: level=1, tier=1 → 1; level=100, tier=1 → 100; level=1, tier=2 → 101; level=19, tier=3 → 219.
        /// </summary>
        public static int GetEffectiveDepth(int pitLevel, int pitTier)
        {
            int clampedTier = pitTier < 1 ? 1 : (pitTier > MaxPitTier ? MaxPitTier : pitTier);
            return (clampedTier - 1) * CycleLength + pitLevel;
        }

        /// <summary>
        /// Returns the displayed (cycle) pit level for a cumulative depth value.
        /// Guard: depth less than 1 returns 1.
        /// Example: depth=100 → 100 (tier 1); depth=101 → 1 (tier 2); depth=219 → 19 (tier 3).
        /// </summary>
        public static int GetDisplayedLevelForDepth(int depth)
        {
            if (depth < 1) return 1;
            return ((depth - 1) % CycleLength) + 1;
        }

        /// <summary>
        /// Returns the pit tier for a cumulative depth value.
        /// Guard: depth less than 1 returns 1. Result is capped at MaxPitTier.
        /// Example: depth=100 → 1; depth=101 → 2; depth=219 → 3.
        /// </summary>
        public static int GetTierForDepth(int depth)
        {
            if (depth < 1) return 1;
            int tier = (depth - 1) / CycleLength + 1;
            return tier > MaxPitTier ? MaxPitTier : tier;
        }

        /// <summary>The level within the current biome (1–25) for a displayed cycle level. Guard: below 1 returns 1.</summary>
        public static int GetBiomeLevel(int pitLevel)
        {
            if (pitLevel < 1) return 1;
            return ((pitLevel - 1) % BiomeLength) + 1;
        }

        /// <summary>The biome a displayed cycle level belongs to (levels past the cycle wrap).</summary>
        public static PitBiome GetBiome(int pitLevel)
        {
            if (pitLevel < 1) return PitBiome.Cave;
            int index = ((pitLevel - 1) % CycleLength) / BiomeLength;
            return (PitBiome)index;
        }

        /// <summary>True when the displayed cycle level is a boss floor of its biome (biome levels 5/10/15/20/25).</summary>
        public static bool IsBossFloor(int pitLevel)
        {
            return CaveBiomeConfig.IsBossFloor(GetBiomeLevel(pitLevel));
        }

        /// <summary>
        /// Scaling tier for loot (tier-scaled "+N" gear and chest gold): one step per biome of effective depth,
        /// capped at <see cref="MaxScalingTier"/>. Example: level 1–25 tier 1 → 1; level 26 → 2; level 100 → 4;
        /// level 1 tier 2 → 5.
        /// </summary>
        public static int GetScalingTier(int pitLevel, int pitTier)
        {
            return GetScalingTierForDepth(GetEffectiveDepth(pitLevel, pitTier));
        }

        /// <summary>Scaling tier for a cumulative depth (see <see cref="GetScalingTier"/>).</summary>
        public static int GetScalingTierForDepth(int depth)
        {
            if (depth < 1) return 1;
            int tier = (depth - 1) / BiomeLength + 1;
            return tier > MaxScalingTier ? MaxScalingTier : tier;
        }

        /// <summary>Extra effective depth a scaling tier adds over the first biome, fed to Gear.CreateTierScaledCopy.</summary>
        public static int GetScalingDepthDelta(int scalingTier)
        {
            return (scalingTier < 1 ? 0 : scalingTier - 1) * BiomeLength;
        }

        /// <summary>
        /// Converts a pit position saved before issue #473, when every 25-level biome loop was its own
        /// tier, to the 100-level cycle. The effective depth is preserved, so the party lands on the same
        /// floor difficulty: legacy (level 1, tier 2) → (level 26, tier 1).
        /// </summary>
        public static void ConvertLegacyPitPosition(int legacyLevel, int legacyTier, out int pitLevel, out int pitTier)
        {
            if (legacyLevel < 1) legacyLevel = 1;
            if (legacyTier < 1) legacyTier = 1;
            int depth = (legacyTier - 1) * BiomeLength + legacyLevel;
            pitLevel = GetDisplayedLevelForDepth(depth);
            pitTier = GetTierForDepth(depth);
        }
    }
}
