namespace PitHero.AI
{
    /// <summary>
    /// States for the Hero State Machine
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