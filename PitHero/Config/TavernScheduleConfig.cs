using PitHero.Dining;

namespace PitHero.Config
{
    /// <summary>
    /// Single source of truth for tavern schedule constants (issue #392):
    /// kitchen hours, meal-period boundaries, and patron arrival-rate multipliers.
    /// Pure static — no Nez or Core dependency, fully headless-testable.
    /// </summary>
    public static class TavernScheduleConfig
    {
        // ── Kitchen hours ────────────────────────────────────────────────────────

        /// <summary>Hour at which the kitchen opens and begins taking orders (inclusive).</summary>
        public const int KitchenOpenHour = 6;

        /// <summary>Hour at which the kitchen stops taking new orders (inclusive at and above).</summary>
        public const int KitchenCloseHour = 22;

        // ── Meal-period start hours ──────────────────────────────────────────────

        /// <summary>In-game hour that triggers the breakfast meal period.</summary>
        public const int BreakfastHour = 6;

        /// <summary>In-game hour that triggers the lunch meal period.</summary>
        public const int LunchHour = 12;

        /// <summary>In-game hour that triggers the dinner meal period.</summary>
        public const int DinnerHour = 18;

        // ── Queries ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the kitchen is closed and should not accept new orders.
        /// Closed window: 10 PM (22) through 5 AM (inclusive).
        /// </summary>
        public static bool IsKitchenClosed(int hour) => hour >= KitchenCloseHour || hour < KitchenOpenHour;

        /// <summary>
        /// Returns the patron-arrival interval multiplier for the given in-game hour.
        /// Rush windows [6,8), [12,14), [18,21) receive double the base rate (0.5× interval);
        /// all other hours including overnight receive the slow-trickle rate (2× interval).
        /// </summary>
        public static float GetArrivalIntervalMultiplier(int hour)
        {
            if (hour >= 6  && hour < 8)  return 0.5f;
            if (hour >= 12 && hour < 14) return 0.5f;
            if (hour >= 18 && hour < 21) return 0.5f;
            return 2f;
        }

        /// <summary>
        /// Maps an in-game hour to its corresponding meal period.
        /// Returns true at hours 6 (Breakfast), 12 (Lunch), and 18 (Dinner).
        /// Returns false for all other hours.
        /// </summary>
        public static bool TryGetMealAtHour(int hour, out MealPeriod meal)
        {
            switch (hour)
            {
                case BreakfastHour:
                    meal = MealPeriod.Breakfast;
                    return true;
                case LunchHour:
                    meal = MealPeriod.Lunch;
                    return true;
                case DinnerHour:
                    meal = MealPeriod.Dinner;
                    return true;
                default:
                    meal = default;
                    return false;
            }
        }
    }
}
