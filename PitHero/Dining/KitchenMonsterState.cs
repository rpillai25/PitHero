namespace PitHero.Dining
{
    /// <summary>States for a monster working in the kitchen or serving in the tavern.</summary>
    public enum KitchenMonsterState
    {
        /// <summary>Monster is stepping out of its house door.</summary>
        EmergeFromHouse,
        /// <summary>Walking to the assigned work post (stove, server area, runner area).</summary>
        WalkToPost,
        /// <summary>Standing at the post waiting for a task.</summary>
        IdleAtPost,
        /// <summary>Cook: cooking a claimed ticket at the stove.</summary>
        Cooking,
        /// <summary>Server: walking to the stove to pick up a plated dish.</summary>
        WalkToPickUpDish,
        /// <summary>Server: carrying a dish to the patron's table.</summary>
        DeliveringDish,
        /// <summary>Server: walking to a patron to take their order.</summary>
        WalkToTakeOrder,
        /// <summary>Server: walking to plate for a bus job, or walking to sink to drop it.</summary>
        BusingPlate,
        /// <summary>Runner: walking to the nearest Crop Storage to fetch ingredients.</summary>
        WalkToStorage,
        /// <summary>Runner: brief collect wait at the storage door.</summary>
        CollectingIngredients,
        /// <summary>Runner: returning to the kitchen (sink tile area) after fetching.</summary>
        ReturnToKitchen,
        /// <summary>Monster walking back to its house to despawn.</summary>
        ReturnHome,
    }

    /// <summary>Role assigned to a kitchen worker.</summary>
    public enum KitchenRole
    {
        Cook,
        Server,
        Runner,
    }
}
