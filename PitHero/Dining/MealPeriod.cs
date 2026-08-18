namespace PitHero.Dining
{
    /// <summary>
    /// The three scheduled meal periods the party can auto-dine during (issue #392).
    /// Breakfast is triggered from SleepInBedAction after waking; Lunch and Dinner
    /// are triggered by the in-game-hour edge watcher in MainGameScene.
    /// </summary>
    public enum MealPeriod
    {
        Breakfast,
        Lunch,
        Dinner,
    }
}
