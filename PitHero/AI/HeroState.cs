namespace PitHero.AI
{
    /// <summary>
    /// Actor state for the simplified state machine
    /// </summary>
    public enum ActorState
    {
        Idle,
        GoTo,
        PerformAction
    }

    /// <summary>
    /// Location types for the state machine queue
    /// </summary>
    public enum LocationType
    {
        None,
        PitOutsideEdge,
        PitInsideEdge,
        PitWanderPoint,
        WizardOrb,
        PitRegenPoint,
        TownWanderPoint,
        Inn,
        ItemShop,
        WeaponShop,
        ArmorShop
    }

    /// <summary>
    /// Legacy hero states (kept for backward compatibility during transition)
    /// </summary>
    public enum HeroState
    {
        /// <summary>
        /// Hero is idle and planning next actions using GOAP
        /// </summary>
        Idle,
        
        /// <summary>
        /// Hero is moving to a destination (SpawningPoint, PitAdjacentSquare, etc.)
        /// </summary>
        GoTo,
        
        /// <summary>
        /// Hero is performing a specific GOAP action (MoveToPit, JumpIntoPit, Wander)
        /// </summary>
        PerformAction
    }
}