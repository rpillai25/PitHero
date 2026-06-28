namespace PitHero.Farming
{
    /// <summary>States for a monster working the farm.</summary>
    public enum FarmingMonsterState
    {
        EmergeFromHouse,
        Idle,
        MoveToTask,
        PerformTill,
        PerformPlant,
        PerformWater,
        FillWateringCan,
        PerformHarvest,
        CarryHarvestToStorage,
        Wander,
        ReturnHome
    }
}
