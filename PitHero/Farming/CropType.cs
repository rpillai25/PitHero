namespace PitHero.Farming
{
    /// <summary>Identifies the type of crop that can be planted on a tilled tile.</summary>
    public enum CropType
    {
        AppleTree  = 0,
        Corn       = 1,
        Eggplant   = 2,
        Grapes     = 3,
        Lettuce    = 4,
        Onion      = 5,
        Potato     = 6,
        Pumpkin    = 7,
        Sugarcane  = 8,
        Tomato     = 9,
        Turnip     = 10,
        Watermelon = 11,
        Wheat      = 12,
    }

    /// <summary>Compile-time constants for the CropType enum.</summary>
    public static class CropTypeInfo
    {
        /// <summary>Total number of distinct crop types.</summary>
        public const int Count = 13;
    }
}
