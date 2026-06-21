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
    }
}
