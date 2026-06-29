using PitHero.Farming;

namespace PitHero.Util
{
    /// <summary>Static lookup helpers for crop display names, atlas prefixes, and frame indices.</summary>
    public static class CropConfig
    {
        /// <summary>Returns the human-readable display name for a crop type.</summary>
        public static string GetDisplayName(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => "Apple Tree",
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

        /// <summary>Returns a short flavor description for a crop, shown in the Harvested Crops viewer.</summary>
        public static string GetDescription(CropType crop)
        {
            return crop switch
            {
                CropType.AppleTree  => "Crisp orchard apples picked from a mature tree.",
                CropType.Corn       => "Sweet golden ears, great roasted or milled.",
                CropType.Eggplant   => "Glossy purple fruit with a hearty bite.",
                CropType.Grapes     => "Plump clusters prized for juice and wine.",
                CropType.Lettuce    => "Crunchy leafy heads, fresh and light.",
                CropType.Onion      => "Pungent bulbs that flavor any dish.",
                CropType.Potato     => "Starchy tubers, filling and versatile.",
                CropType.Pumpkin    => "Big autumn squash with a sweet flesh.",
                CropType.Sugarcane  => "Fibrous stalks pressed for sweet syrup.",
                CropType.Tomato     => "Juicy red fruit bursting with flavor.",
                CropType.Turnip     => "Humble roots, quick to grow and store.",
                CropType.Watermelon => "Huge melons full of cool, sweet water.",
                CropType.Wheat      => "Golden grain, the staple of every loaf.",
                _                   => GetDisplayName(crop),
            };
        }
    }
}
