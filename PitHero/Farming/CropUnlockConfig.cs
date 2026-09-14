namespace PitHero.Farming
{
    /// <summary>One "harvest N of this crop" condition toward unlocking a crop.</summary>
    public struct CropUnlockRequirement
    {
        /// <summary>The crop that must have been harvested.</summary>
        public CropType Crop;

        /// <summary>Lifetime harvested units required (GameStateService.CropHarvestedTotals).</summary>
        public int Required;

        public CropUnlockRequirement(CropType crop, int required)
        {
            Crop = crop;
            Required = required;
        }
    }

    /// <summary>
    /// Crop progression (issue #413): crops unlock in tiers as the lifetime harvest totals of earlier
    /// crops grow. Wheat and Corn start unlocked; every later tier lists exact per-crop totals that
    /// must ALL be met. Pure and headless-testable; the totals array is indexed by (int)CropType.
    /// The <see cref="CropType"/> enum is alphabetical and persisted, so the progression order lives
    /// here and never in the enum.
    /// </summary>
    public static class CropUnlockConfig
    {
        /// <summary>Crops in progression (display) order: the starting pair first, Apple Tree last.</summary>
        public static readonly CropType[] ProgressionOrder =
        {
            CropType.Wheat, CropType.Corn,
            CropType.Tomato, CropType.Eggplant,
            CropType.Sugarcane, CropType.Lettuce,
            CropType.Turnip, CropType.Onion,
            CropType.Potato, CropType.Grapes,
            CropType.Watermelon, CropType.Pumpkin,
            CropType.AppleTree,
        };

        private static readonly CropUnlockRequirement[] Tier0 = new CropUnlockRequirement[0];

        private static readonly CropUnlockRequirement[] Tier1 =
        {
            new CropUnlockRequirement(CropType.Wheat, 9),
            new CropUnlockRequirement(CropType.Corn, 9),
        };

        private static readonly CropUnlockRequirement[] Tier2 =
        {
            new CropUnlockRequirement(CropType.Wheat, 18),
            new CropUnlockRequirement(CropType.Corn, 18),
            new CropUnlockRequirement(CropType.Tomato, 12),
            new CropUnlockRequirement(CropType.Eggplant, 12),
        };

        private static readonly CropUnlockRequirement[] Tier3 =
        {
            new CropUnlockRequirement(CropType.Wheat, 27),
            new CropUnlockRequirement(CropType.Corn, 27),
            new CropUnlockRequirement(CropType.Tomato, 24),
            new CropUnlockRequirement(CropType.Eggplant, 24),
            new CropUnlockRequirement(CropType.Sugarcane, 16),
            new CropUnlockRequirement(CropType.Lettuce, 16),
        };

        private static readonly CropUnlockRequirement[] Tier4 =
        {
            new CropUnlockRequirement(CropType.Wheat, 36),
            new CropUnlockRequirement(CropType.Corn, 36),
            new CropUnlockRequirement(CropType.Tomato, 36),
            new CropUnlockRequirement(CropType.Eggplant, 36),
            new CropUnlockRequirement(CropType.Sugarcane, 32),
            new CropUnlockRequirement(CropType.Lettuce, 32),
            new CropUnlockRequirement(CropType.Turnip, 20),
            new CropUnlockRequirement(CropType.Onion, 20),
        };

        private static readonly CropUnlockRequirement[] Tier5 =
        {
            new CropUnlockRequirement(CropType.Wheat, 45),
            new CropUnlockRequirement(CropType.Corn, 45),
            new CropUnlockRequirement(CropType.Tomato, 48),
            new CropUnlockRequirement(CropType.Eggplant, 48),
            new CropUnlockRequirement(CropType.Sugarcane, 48),
            new CropUnlockRequirement(CropType.Lettuce, 48),
            new CropUnlockRequirement(CropType.Turnip, 40),
            new CropUnlockRequirement(CropType.Onion, 40),
            new CropUnlockRequirement(CropType.Potato, 24),
            new CropUnlockRequirement(CropType.Grapes, 24),
        };

        private static readonly CropUnlockRequirement[] Tier6 =
        {
            new CropUnlockRequirement(CropType.Wheat, 54),
            new CropUnlockRequirement(CropType.Corn, 54),
            new CropUnlockRequirement(CropType.Tomato, 60),
            new CropUnlockRequirement(CropType.Eggplant, 60),
            new CropUnlockRequirement(CropType.Sugarcane, 64),
            new CropUnlockRequirement(CropType.Lettuce, 64),
            new CropUnlockRequirement(CropType.Turnip, 60),
            new CropUnlockRequirement(CropType.Onion, 60),
            new CropUnlockRequirement(CropType.Potato, 48),
            new CropUnlockRequirement(CropType.Grapes, 48),
            new CropUnlockRequirement(CropType.Watermelon, 20),
            new CropUnlockRequirement(CropType.Pumpkin, 20),
        };

        /// <summary>Requirement table per tier; tier N unlocks ProgressionOrder[2N] and [2N+1] (tier 6 = Apple Tree alone).</summary>
        private static readonly CropUnlockRequirement[][] TierRequirements = { Tier0, Tier1, Tier2, Tier3, Tier4, Tier5, Tier6 };

        /// <summary>Number of unlock tiers (tier 0 is the free starting pair).</summary>
        public static int TierCount => TierRequirements.Length;

        /// <summary>The progression tier a crop belongs to (0 = unlocked from the start).</summary>
        public static int GetTier(CropType crop)
        {
            for (int i = 0; i < ProgressionOrder.Length; i++)
            {
                if (ProgressionOrder[i] == crop)
                    return i / 2;
            }
            return 0;
        }

        /// <summary>The harvest totals required before the crop unlocks (empty for starting crops).</summary>
        public static CropUnlockRequirement[] GetRequirements(CropType crop)
        {
            int tier = GetTier(crop);
            return tier >= 0 && tier < TierRequirements.Length ? TierRequirements[tier] : Tier0;
        }

        /// <summary>
        /// True when every requirement of the crop's tier is met by <paramref name="harvestedTotals"/>
        /// (indexed by (int)CropType). A null or short array counts as zero harvested.
        /// </summary>
        public static bool IsUnlocked(CropType crop, int[] harvestedTotals)
        {
            var reqs = GetRequirements(crop);
            for (int i = 0; i < reqs.Length; i++)
            {
                if (GetTotal(harvestedTotals, reqs[i].Crop) < reqs[i].Required)
                    return false;
            }
            return true;
        }

        /// <summary>Bit per unlocked crop ((int)CropType), for cheap before/after comparisons.</summary>
        public static int GetUnlockedMask(int[] harvestedTotals)
        {
            int mask = 0;
            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                if (IsUnlocked((CropType)i, harvestedTotals))
                    mask |= 1 << i;
            }
            return mask;
        }

        /// <summary>Lifetime harvested units for a crop from a totals array (0 when missing).</summary>
        public static int GetTotal(int[] harvestedTotals, CropType crop)
        {
            int i = (int)crop;
            return harvestedTotals != null && i >= 0 && i < harvestedTotals.Length ? harvestedTotals[i] : 0;
        }
    }
}
