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


}