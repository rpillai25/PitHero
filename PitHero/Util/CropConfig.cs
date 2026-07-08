using PitHero.Farming;

namespace PitHero.Util
{
    /// <summary>Static lookup helpers for crop display names, atlas prefixes, and frame indices.</summary>
    public static class CropConfig
    {
        /// <summary>Localization key (UI text) for a crop type's display name. Resolve via TextService.</summary>
        public static string GetDisplayNameKey(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => UITextKey.CropNameAppleTree,
                CropType.Corn       => UITextKey.CropNameCorn,
                CropType.Eggplant   => UITextKey.CropNameEggplant,
                CropType.Grapes     => UITextKey.CropNameGrapes,
                CropType.Lettuce    => UITextKey.CropNameLettuce,
                CropType.Onion      => UITextKey.CropNameOnion,
                CropType.Potato     => UITextKey.CropNamePotato,
                CropType.Pumpkin    => UITextKey.CropNamePumpkin,
                CropType.Sugarcane  => UITextKey.CropNameSugarcane,
                CropType.Tomato     => UITextKey.CropNameTomato,
                CropType.Turnip     => UITextKey.CropNameTurnip,
                CropType.Watermelon => UITextKey.CropNameWatermelon,
                CropType.Wheat      => UITextKey.CropNameWheat,
                _                   => UITextKey.CropNameAppleTree,
            };
        }

        /// <summary>Returns the atlas prefix string used in sprite names for this crop (matches enum name exactly).</summary>
        public static string GetAtlasPrefix(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => "AppleTree",
                CropType.Corn       => "Corn",
                CropType.Eggplant   => "Eggplant",
                CropType.Grapes     => "Grapes",
                CropType.Lettuce    => "Lettuce",
                CropType.Onion      => "Onion",
                CropType.Potato     => "Potato",
                CropType.Pumpkin    => "Pumpkin",
                CropType.Sugarcane  => "Sugarcane",
                CropType.Tomato     => "Tomato",
                CropType.Turnip     => "Turnip",
                CropType.Watermelon => "Watermelon",
                CropType.Wheat      => "Wheat",
                _                   => crop.ToString(),
            };
        }

        /// <summary>Returns the sprite name for frame 1 (seed stage) of a crop.</summary>
        public static string GetSeedSpriteName(CropType crop)
        {
            return GetAtlasPrefix(crop) + "_1";
        }

        /// <summary>Returns the sprite name for the last frame (fully grown) of a crop.</summary>
        public static string GetFullyGrownSpriteName(CropType crop)
        {
            int lastFrame = crop switch
            {
                CropType.AppleTree  => 11,
                CropType.Corn       => 10,
                CropType.Eggplant   => 11,
                CropType.Grapes     => 9,
                CropType.Lettuce    => 5,
                CropType.Onion      => 6,
                CropType.Potato     => 7,
                CropType.Pumpkin    => 11,
                CropType.Sugarcane  => 8,
                CropType.Tomato     => 9,
                CropType.Turnip     => 7,
                CropType.Watermelon => 12,
                CropType.Wheat      => 5,
                _                   => 1,
            };
            return GetAtlasPrefix(crop) + "_" + lastFrame;
        }

        /// <summary>Returns the total number of growth frames (= stages) for a crop.</summary>
        public static int GetFrameCount(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => 11,
                CropType.Corn       => 10,
                CropType.Eggplant   => 11,
                CropType.Grapes     => 9,
                CropType.Lettuce    => 5,
                CropType.Onion      => 6,
                CropType.Potato     => 7,
                CropType.Pumpkin    => 11,
                CropType.Sugarcane  => 8,
                CropType.Tomato     => 9,
                CropType.Turnip     => 7,
                CropType.Watermelon => 12,
                CropType.Wheat      => 5,
                _                   => 1,
            };
        }

        /// <summary>Returns the in-game hours required per growth stage for a crop.</summary>
        public static float GetHoursPerStage(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => 16f,
                CropType.Corn       => 3f,
                CropType.Eggplant   => 6f,
                CropType.Grapes     => 4f,
                CropType.Lettuce    => 2f,
                CropType.Onion      => 4f,
                CropType.Potato     => 3f,
                CropType.Pumpkin    => 8f,
                CropType.Sugarcane  => 3f,
                CropType.Tomato     => 3f,
                CropType.Turnip     => 2f,
                CropType.Watermelon => 10f,
                CropType.Wheat      => 2f,
                _                   => 4f,
            };
        }

        /// <summary>Returns the atlas sprite name for a specific growth frame (1-indexed).</summary>
        public static string GetFrameSpriteName(CropType crop, int frame)
        {
            return GetAtlasPrefix(crop) + "_" + frame;
        }

        /// <summary>
        /// Localization key (UI text) for a crop's harvested-product display name. An Apple Tree
        /// yields "Apple"; every other crop's product shares its own name. Resolve via TextService.
        /// </summary>
        public static string GetHarvestDisplayNameKey(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => UITextKey.HarvestCropApple,
                CropType.Corn       => UITextKey.HarvestCropCorn,
                CropType.Eggplant   => UITextKey.HarvestCropEggplant,
                CropType.Grapes     => UITextKey.HarvestCropGrapes,
                CropType.Lettuce    => UITextKey.HarvestCropLettuce,
                CropType.Onion      => UITextKey.HarvestCropOnion,
                CropType.Potato     => UITextKey.HarvestCropPotato,
                CropType.Pumpkin    => UITextKey.HarvestCropPumpkin,
                CropType.Sugarcane  => UITextKey.HarvestCropSugarcane,
                CropType.Tomato     => UITextKey.HarvestCropTomato,
                CropType.Turnip     => UITextKey.HarvestCropTurnip,
                CropType.Watermelon => UITextKey.HarvestCropWatermelon,
                CropType.Wheat      => UITextKey.HarvestCropWheat,
                _                   => UITextKey.HarvestCropApple,
            };
        }

        /// <summary>Returns the CropsProps atlas sprite name for a crop's harvested product.</summary>
        public static string GetHarvestSpriteName(CropType crop)
        {
            // The harvested-apple sprite is named "Apple_Harvest", not "AppleTree_Harvest".
            string prefix = crop == CropType.AppleTree ? "Apple" : GetAtlasPrefix(crop);
            return prefix + "_Harvest";
        }

        /// <summary>Number of harvested units produced each time this crop is harvested.</summary>
        public static int GetHarvestYield(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => 4,
                CropType.Corn       => 3,
                CropType.Eggplant   => 1,
                CropType.Grapes     => 1,
                CropType.Lettuce    => 4,
                CropType.Onion      => 9,
                CropType.Potato     => 4,
                CropType.Pumpkin    => 1,
                CropType.Sugarcane  => 2,
                CropType.Tomato     => 4,
                CropType.Turnip     => 9,
                CropType.Watermelon => 1,
                CropType.Wheat      => 1,
                _                   => 1,
            };
        }

        /// <summary>Maximum number of a harvested crop that can be held in a single storage slot.</summary>
        public static int GetMaxHarvestStack(CropType crop)
        {
            return crop switch
            {
                CropType.Grapes     => 100,
                CropType.Eggplant   => 30,
                CropType.Pumpkin    => 10,
                CropType.Watermelon => 10,
                CropType.Onion      => 30,
                CropType.Turnip     => 50,
                CropType.Lettuce    => 30,
                CropType.Sugarcane  => 20,
                CropType.Corn       => 20,
                CropType.Potato     => 30,
                CropType.Wheat      => 100,
                CropType.Tomato     => 30,
                CropType.AppleTree  => 30,
                _                   => 30,
            };
        }

        /// <summary>
        /// True for crops that regrow after harvesting (revert to an earlier frame) instead of
        /// being permanently removed from the field.
        /// </summary>
        public static bool IsRepeatHarvest(CropType crop)
        {
            return crop switch
            {
                CropType.Grapes    => true,
                CropType.Eggplant  => true,
                CropType.Corn      => true,
                CropType.Tomato    => true,
                CropType.AppleTree => true,
                _                  => false,
            };
        }

        /// <summary>
        /// Frame a repeat-harvest crop reverts to after harvesting (only meaningful when
        /// <see cref="IsRepeatHarvest"/> is true). The crop regrows from this frame to fully grown.
        /// </summary>
        public static int GetRevertFrame(CropType crop)
        {
            return crop switch
            {
                CropType.Grapes    => 0,
                CropType.Eggplant  => 6,
                CropType.Corn      => 7,
                CropType.Tomato    => 5,
                CropType.AppleTree => 10,
                _                  => 1,
            };
        }

        /// <summary>Gold cost to purchase one seed of this crop from the Second Chance Shop.</summary>
        public static int GetSeedPrice(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => 200,
                CropType.Corn       => 50,
                CropType.Eggplant   => 50,
                CropType.Grapes     => 50,
                CropType.Lettuce    => 50,
                CropType.Onion      => 50,
                CropType.Potato     => 50,
                CropType.Pumpkin    => 100,
                CropType.Sugarcane  => 50,
                CropType.Tomato     => 50,
                CropType.Turnip     => 50,
                CropType.Watermelon => 100,
                CropType.Wheat      => 25,
                _                   => 50,
            };
        }

        /// <summary>
        /// Per-stage time multiplier applied while a repeat-harvest crop regrows. 1.0 = normal
        /// rate; 1.5 = slower regrowth (Corn, Tomato, AppleTree).
        /// </summary>
        public static float GetRegrowthRateMultiplier(CropType crop)
        {
            return crop switch
            {
                CropType.Corn      => 1.5f,
                CropType.Tomato    => 1.5f,
                CropType.AppleTree => 1.5f,
                _                  => 1f,
            };
        }

        /// <summary>
        /// Relative growth-time tier used by the harvest sell-price formula. Higher tiers (slower
        /// crops) pay more per harvested unit. Revisit if grow times are rebalanced; the rest of the
        /// sell formula stays unchanged. See issue #285.
        /// </summary>
        public static float GetGrowthTier(CropType crop)
        {
            return crop switch
            {
                CropType.Wheat      => 0.85f,
                CropType.Lettuce    => 0.90f,
                CropType.Sugarcane  => 0.90f,
                CropType.Turnip     => 0.90f,
                CropType.Onion      => 1.00f,
                CropType.Potato     => 1.00f,
                CropType.Eggplant   => 1.20f,
                CropType.Tomato     => 1.20f,
                CropType.Corn       => 1.30f,
                CropType.Grapes     => 1.40f,
                CropType.Pumpkin    => 1.40f,
                CropType.Watermelon => 1.40f,
                CropType.AppleTree  => 1.80f,
                _                   => 1.00f,
            };
        }

        /// <summary>
        /// Sell value of a single harvested unit of this crop:
        /// <c>max((seed_price × growth_tier × 1.25) / harvest_yield, 5)</c>. The 1.25 profit scalar
        /// gives a tier-1.0 crop +25% ROI on a full harvest; the floor of 5 guards against unusual
        /// seed/yield combos. See issue #285.
        /// </summary>
        public static float GetHarvestUnitSellPrice(CropType crop)
        {
            float raw = (GetSeedPrice(crop) * GetGrowthTier(crop) * 1.25f) / GetHarvestYield(crop);
            return raw < 5f ? 5f : raw;
        }

        /// <summary>
        /// Gold paid for selling a stack of <paramref name="count"/> harvested units:
        /// <c>ceil(unit_sell_price × count)</c>.
        /// </summary>
        public static int GetHarvestStackSellPrice(CropType crop, int count)
        {
            return (int)System.Math.Ceiling(GetHarvestUnitSellPrice(crop) * count);
        }

        /// <summary>
        /// Localization key (UI text) for a crop's flavor description, shown in the Harvested Crops
        /// viewer. Resolve via TextService.
        /// </summary>
        public static string GetDescriptionKey(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => UITextKey.CropDescAppleTree,
                CropType.Corn       => UITextKey.CropDescCorn,
                CropType.Eggplant   => UITextKey.CropDescEggplant,
                CropType.Grapes     => UITextKey.CropDescGrapes,
                CropType.Lettuce    => UITextKey.CropDescLettuce,
                CropType.Onion      => UITextKey.CropDescOnion,
                CropType.Potato     => UITextKey.CropDescPotato,
                CropType.Pumpkin    => UITextKey.CropDescPumpkin,
                CropType.Sugarcane  => UITextKey.CropDescSugarcane,
                CropType.Tomato     => UITextKey.CropDescTomato,
                CropType.Turnip     => UITextKey.CropDescTurnip,
                CropType.Watermelon => UITextKey.CropDescWatermelon,
                CropType.Wheat      => UITextKey.CropDescWheat,
                _                   => UITextKey.CropDescAppleTree,
            };
        }
    }
}
