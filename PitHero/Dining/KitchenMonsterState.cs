namespace PitHero.Dining
{
    /// <summary>States for a monster working in the kitchen or serving in the tavern.</summary>
    public enum KitchenMonsterState
    {
        /// <summary>Monster is stepping out of its house door.</summary>
        EmergeFromHouse,
        /// <summary>Monster walking back to its house to despawn.</summary>
        ReturnHome,

        // ── Server ──────────────────────────────────────────────────────────────
        /// <summary>Server deciding what to do next (also the landing state after emerging).</summary>
        ServerDecide,
        /// <summary>Server walking to a patron (or party seat) to take an order.</summary>
        ServerWalkToPatron,
        /// <summary>Server walking to the ticket board with taken orders in memory.</summary>
        ServerWalkToBoard,
        /// <summary>Server pausing at the board to post tickets.</summary>
        ServerPostTickets,
        /// <summary>Server walking to the serving tables to pick up cooked dishes.</summary>
        ServerWalkToPickup,
        /// <summary>Server carrying dishes to seat tables (delivers sequentially, then sinks orphans).</summary>
        ServerDeliver,
        /// <summary>Server carrying a canceled/orphaned dish to the sink.</summary>
        ServerWalkToSink,
        /// <summary>Server walking to a finished plate, then carrying it to the sink.</summary>
        ServerBusPlate,
        /// <summary>Server wandering within its table area.</summary>
        ServerWander,

        // ── Cook ────────────────────────────────────────────────────────────────
        /// <summary>Cook walking to the ticket board.</summary>
        CookWalkToBoard,
        /// <summary>Cook at the board: waiting for a posted ticket, then a 1s read pause.</summary>
        CookAtBoard,
        /// <summary>Cook walking to the fridge to gather ingredients.</summary>
        CookWalkToFridge,
        /// <summary>Cook waiting at the fridge for the runner to bring missing ingredients.</summary>
        CookWaitIngredients,
        /// <summary>Cook walking to its claimed cooking station.</summary>
        CookWalkToStation,
        /// <summary>Cook preparing the dish at the station.</summary>
        CookCooking,
        /// <summary>Cook carrying the finished dish to a serving table.</summary>
        CookWalkToServing,
        /// <summary>All serving tables are full — cook holds the dish until a slot frees.</summary>
        CookWaitServingSlot,

        // ── Runner ──────────────────────────────────────────────────────────────
        /// <summary>Runner idling at its post waiting for a fetch job.</summary>
        RunnerIdle,
        /// <summary>Runner walking to the nearest Crop Storage.</summary>
        RunnerWalkToStorage,
        /// <summary>Runner collecting at the storage door (brief pause).</summary>
        RunnerCollect,
        /// <summary>Runner carrying ingredients back to the fridge.</summary>
        RunnerWalkToFridge,
    }

    /// <summary>Role assigned to a kitchen worker.</summary>
    public enum KitchenRole
    {
        Cook,
        Server,
        Runner,
    }

    /// <summary>Which tavern tables a server currently works.</summary>
    public enum ServerZone
    {
        /// <summary>Single server on shift: works all 4 tables.</summary>
        AllTables,
        /// <summary>First of two servers: the two top tables (93,3) and (97,3).</summary>
        TopTables,
        /// <summary>Second of two servers: the two bottom tables (93,7) and (97,7).</summary>
        BottomTables,
    }
}
