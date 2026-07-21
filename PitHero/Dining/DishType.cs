namespace PitHero.Dining
{
    /// <summary>Identifies a dish the kitchen can cook. Persisted as int — values must stay stable.</summary>
    public enum DishType
    {
        RoastedOnionSkewers     = 0,
        TurnipOnionStew         = 1,
        ButteredBread           = 2,
        CheesyMashedPotatoes    = 3,
        GardenSalad             = 4,
        GrilledCornWithButter   = 5,
        TomatoCheeseBisque      = 6,
        CornChowder             = 7,
        EggplantParmesan        = 8,
        GrapeJuice              = 9,
        GrapeTart               = 10,
        SpicedEggplantSteak     = 11,
        ApplePie                = 12,
        PumpkinCreamSoup        = 13,
        ChilledWatermelonSorbet = 14,
        HarvestFeastPlatter     = 15,
    }

    /// <summary>Compile-time constants for the DishType enum.</summary>
    public static class DishTypeInfo
    {
        /// <summary>Total number of distinct dish types.</summary>
        public const int Count = 16;
    }
}
