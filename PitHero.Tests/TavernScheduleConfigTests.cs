using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.Dining;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for TavernScheduleConfig (issue #392): kitchen-closed window, patron arrival
    /// interval multipliers, and meal-period boundary detection.
    /// </summary>
    [TestClass]
    public class TavernScheduleConfigTests
    {
        // ── IsKitchenClosed ──────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void IsKitchenClosed_AllHours_CorrectOpenAndClosedWindows()
        {
            // Open: hours 6 through 21 inclusive
            for (int hour = 6; hour < 22; hour++)
                Assert.IsFalse(TavernScheduleConfig.IsKitchenClosed(hour),
                    $"Hour {hour} should be open");

            // Closed: hours 22, 23, 0 through 5
            for (int hour = 22; hour <= 23; hour++)
                Assert.IsTrue(TavernScheduleConfig.IsKitchenClosed(hour),
                    $"Hour {hour} should be closed");
            for (int hour = 0; hour < 6; hour++)
                Assert.IsTrue(TavernScheduleConfig.IsKitchenClosed(hour),
                    $"Hour {hour} should be closed");
        }

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void IsKitchenClosed_BoundaryHours()
        {
            Assert.IsFalse(TavernScheduleConfig.IsKitchenClosed(6),  "6 AM is the open boundary");
            Assert.IsFalse(TavernScheduleConfig.IsKitchenClosed(21), "9 PM is the last open hour");
            Assert.IsTrue(TavernScheduleConfig.IsKitchenClosed(22),  "10 PM is the first closed hour");
            Assert.IsTrue(TavernScheduleConfig.IsKitchenClosed(5),   "5 AM is still closed");
        }

        // ── GetArrivalIntervalMultiplier ─────────────────────────────────────────

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void GetArrivalIntervalMultiplier_RushWindows_Return1f()
        {
            // Morning rush [6, 8)
            Assert.AreEqual(1f, TavernScheduleConfig.GetArrivalIntervalMultiplier(6),  "6 AM is rush");
            Assert.AreEqual(1f, TavernScheduleConfig.GetArrivalIntervalMultiplier(7),  "7 AM is rush");
            // Lunch rush [12, 14)
            Assert.AreEqual(1f, TavernScheduleConfig.GetArrivalIntervalMultiplier(12), "12 PM is rush");
            Assert.AreEqual(1f, TavernScheduleConfig.GetArrivalIntervalMultiplier(13), "1 PM is rush");
            // Dinner rush [18, 21)
            Assert.AreEqual(1f, TavernScheduleConfig.GetArrivalIntervalMultiplier(18), "6 PM is rush");
            Assert.AreEqual(1f, TavernScheduleConfig.GetArrivalIntervalMultiplier(19), "7 PM is rush");
            Assert.AreEqual(1f, TavernScheduleConfig.GetArrivalIntervalMultiplier(20), "8 PM is rush");
        }

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void GetArrivalIntervalMultiplier_OffPeakHours_Return2f()
        {
            // Off-peak between rushes and overnight
            int[] offPeak = { 0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 14, 15, 16, 17, 21, 22, 23 };
            for (int i = 0; i < offPeak.Length; i++)
            {
                int hour = offPeak[i];
                Assert.AreEqual(2f, TavernScheduleConfig.GetArrivalIntervalMultiplier(hour),
                    $"Hour {hour} should be off-peak (2x interval)");
            }
        }

        // ── TryGetMealAtHour ─────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void TryGetMealAtHour_BreakfastHour_ReturnsTrue()
        {
            bool result = TavernScheduleConfig.TryGetMealAtHour(6, out var meal);
            Assert.IsTrue(result);
            Assert.AreEqual(MealPeriod.Breakfast, meal);
        }

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void TryGetMealAtHour_LunchHour_ReturnsTrue()
        {
            bool result = TavernScheduleConfig.TryGetMealAtHour(12, out var meal);
            Assert.IsTrue(result);
            Assert.AreEqual(MealPeriod.Lunch, meal);
        }

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void TryGetMealAtHour_DinnerHour_ReturnsTrue()
        {
            bool result = TavernScheduleConfig.TryGetMealAtHour(18, out var meal);
            Assert.IsTrue(result);
            Assert.AreEqual(MealPeriod.Dinner, meal);
        }

        [TestMethod]
        [TestCategory("TavernSchedule")]
        public void TryGetMealAtHour_NonMealHours_ReturnsFalse()
        {
            int[] nonMeal = { 0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 13, 14, 15, 16, 17, 19, 20, 21, 22, 23 };
            for (int i = 0; i < nonMeal.Length; i++)
            {
                int hour = nonMeal[i];
                bool result = TavernScheduleConfig.TryGetMealAtHour(hour, out _);
                Assert.IsFalse(result, $"Hour {hour} should not be a meal boundary");
            }
        }
    }
}
