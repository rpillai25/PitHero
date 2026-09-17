using PitHero.Farming;

namespace PitHero.Dining
{
    /// <summary>One "serve N of this dish" condition toward fully unlocking a dish.</summary>
    public struct DishUnlockRequirement
    {
        /// <summary>The dish that must have been served.</summary>
        public DishType Dish;

        /// <summary>Lifetime servings required (GameStateService.DishesServedTotals).</summary>
        public int Required;

        public DishUnlockRequirement(DishType dish, int required)
        {
            Dish = dish;
            Required = required;
        }
    }

    /// <summary>
    /// Dish progression (issue #417). A dish's tier is the highest progression tier among its recipe
    /// crops, so the menu grows in step with the field: a dish is <b>soft-unlocked</b> (visible in
    /// the Food tab, faded, with its requirements) once every recipe crop is unlocked, and
    /// <b>fully unlocked</b> (orderable by patrons and the party) once the cooking requirements are
    /// met too — every dish of a lower tier served <c>DishUnlockServingsBase × min(tierGap, DishUnlockTierGapCap)</c>
    /// times. Tier 0 (the Wheat/Corn dishes) is free, parallel to the starting crops. Pure and
    /// headless-testable; totals arrays are indexed by (int)CropType / (int)DishType. The
    /// <see cref="DishType"/> enum is persisted, so the progression order lives here, never in the enum.
    /// </summary>
    public static class DishUnlockConfig
    {
        /// <summary>Dishes in progression (display) order: by crop tier, cheapest first within a tier.</summary>
        public static readonly DishType[] ProgressionOrder =
        {
            DishType.ButteredBread, DishType.GrilledCornWithButter,                                      // tier 0
            DishType.TomatoCheeseBisque, DishType.EggplantParmesan,                                      // tier 1
            DishType.GardenSalad,                                                                        // tier 2
            DishType.RoastedOnionSkewers, DishType.TurnipOnionStew, DishType.SpicedEggplantSteak,        // tier 3
            DishType.CornChowder, DishType.CheesyMashedPotatoes, DishType.GrapeTart, DishType.GrapeJuice, // tier 4
            DishType.PumpkinCreamSoup, DishType.ChilledWatermelonSorbet, DishType.HarvestFeastPlatter,   // tier 5
            DishType.ApplePie,                                                                           // tier 6
        };

        private static readonly int[] _tierByDish;
        private static readonly DishUnlockRequirement[][] _requirementsByDish;
        private static readonly DishUnlockRequirement[] _none = new DishUnlockRequirement[0];

        static DishUnlockConfig()
        {
            _tierByDish = new int[DishTypeInfo.Count];
            for (int d = 0; d < DishTypeInfo.Count; d++)
                _tierByDish[d] = ComputeTier((DishType)d);

            _requirementsByDish = new DishUnlockRequirement[DishTypeInfo.Count][];
            for (int d = 0; d < DishTypeInfo.Count; d++)
                _requirementsByDish[d] = BuildRequirements(_tierByDish[d]);
        }

        /// <summary>Number of dish tiers (mirrors the crop tiers).</summary>
        public static int TierCount => CropUnlockConfig.TierCount;

        /// <summary>The progression tier a dish belongs to (0 = available from the start).</summary>
        public static int GetTier(DishType dish)
        {
            int i = (int)dish;
            return i >= 0 && i < _tierByDish.Length ? _tierByDish[i] : 0;
        }

        /// <summary>Highest crop tier among the dish's recipe entries — the rule the cached tier table is built from.</summary>
        public static int ComputeTier(DishType dish)
        {
            var def = DishConfig.GetDefinition(dish);
            int tier = 0;
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                int cropTier = CropUnlockConfig.GetTier(def.Recipe[i].Crop);
                if (cropTier > tier)
                    tier = cropTier;
            }
            return tier;
        }

        /// <summary>The servings required before the dish fully unlocks (empty for tier-0 dishes).</summary>
        public static DishUnlockRequirement[] GetRequirements(DishType dish)
        {
            int i = (int)dish;
            return i >= 0 && i < _requirementsByDish.Length ? _requirementsByDish[i] : _none;
        }

        /// <summary>Builds the requirement list for a tier: every lower-tier dish, in progression order, escalating with the tier gap.</summary>
        private static DishUnlockRequirement[] BuildRequirements(int tier)
        {
            if (tier <= 0)
                return _none;
            int count = 0;
            for (int i = 0; i < ProgressionOrder.Length; i++)
                if (ComputeTier(ProgressionOrder[i]) < tier)
                    count++;
            var reqs = new DishUnlockRequirement[count];
            int n = 0;
            for (int i = 0; i < ProgressionOrder.Length; i++)
            {
                var dish = ProgressionOrder[i];
                int dishTier = ComputeTier(dish);
                if (dishTier >= tier)
                    continue;
                int gap = tier - dishTier;
                if (gap > GameConfig.DishUnlockTierGapCap) gap = GameConfig.DishUnlockTierGapCap;
                reqs[n++] = new DishUnlockRequirement(dish, GameConfig.DishUnlockServingsBase * gap);
            }
            return reqs;
        }

        /// <summary>True when every recipe crop is unlocked — the dish appears on the menu (faded until fully unlocked).</summary>
        public static bool IsSoftUnlocked(DishType dish, int[] cropHarvestedTotals)
        {
            var def = DishConfig.GetDefinition(dish);
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                if (!CropUnlockConfig.IsUnlocked(def.Recipe[i].Crop, cropHarvestedTotals))
                    return false;
            }
            return true;
        }

        /// <summary>True when every serving requirement of the dish is met by <paramref name="dishesServedTotals"/>.</summary>
        public static bool IsCookingRequirementMet(DishType dish, int[] dishesServedTotals)
        {
            var reqs = GetRequirements(dish);
            for (int i = 0; i < reqs.Length; i++)
            {
                if (GetTotal(dishesServedTotals, reqs[i].Dish) < reqs[i].Required)
                    return false;
            }
            return true;
        }

        /// <summary>True when the dish can be ordered: recipe crops unlocked AND cooking requirements met.</summary>
        public static bool IsFullyUnlocked(DishType dish, int[] cropHarvestedTotals, int[] dishesServedTotals)
        {
            return IsSoftUnlocked(dish, cropHarvestedTotals) && IsCookingRequirementMet(dish, dishesServedTotals);
        }

        /// <summary>Bit per soft-unlocked dish ((int)DishType).</summary>
        public static int GetSoftUnlockedMask(int[] cropHarvestedTotals)
        {
            int mask = 0;
            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                if (IsSoftUnlocked((DishType)i, cropHarvestedTotals))
                    mask |= 1 << i;
            }
            return mask;
        }

        /// <summary>Bit per fully unlocked dish ((int)DishType), for cheap before/after comparisons.</summary>
        public static int GetFullyUnlockedMask(int[] cropHarvestedTotals, int[] dishesServedTotals)
        {
            int mask = 0;
            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                if (IsFullyUnlocked((DishType)i, cropHarvestedTotals, dishesServedTotals))
                    mask |= 1 << i;
            }
            return mask;
        }

        /// <summary>Lifetime servings of a dish from a totals array (0 when missing).</summary>
        public static int GetTotal(int[] dishesServedTotals, DishType dish)
        {
            int i = (int)dish;
            return dishesServedTotals != null && i >= 0 && i < dishesServedTotals.Length ? dishesServedTotals[i] : 0;
        }
    }
}
