using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Farming;

namespace PitHero.Tests
{
    /// <summary>
    /// Farm work durations scale with the worker's farming level (issue #422): tilling 7s -> 3s and
    /// harvesting 5s -> 1s across levels 1-9, monotonically and with the endpoints hit exactly.
    /// </summary>
    [TestClass]
    public class FarmWorkDurationsTests
    {
        private const float Tolerance = 0.0001f;

        [TestMethod]
        [TestCategory("Farming")]
        public void TillDuration_HitsBothEndpointsExactly()
        {
            Assert.AreEqual(7f, FarmWorkDurations.GetTillDuration(1), Tolerance, "level 1 tills in 7 seconds");
            Assert.AreEqual(3f, FarmWorkDurations.GetTillDuration(9), Tolerance, "level 9 tills in 3 seconds");
        }

        [TestMethod]
        [TestCategory("Farming")]
        public void HarvestDuration_HitsBothEndpointsExactly()
        {
            Assert.AreEqual(5f, FarmWorkDurations.GetHarvestDuration(1), Tolerance, "level 1 harvests in 5 seconds");
            Assert.AreEqual(1f, FarmWorkDurations.GetHarvestDuration(9), Tolerance, "level 9 harvests in 1 second");
        }

        [TestMethod]
        [TestCategory("Farming")]
        public void Durations_FallMonotonicallyWithLevel()
        {
            for (int level = GameConfig.MonsterJobLevelMin; level < GameConfig.MonsterJobLevelMax; level++)
            {
                Assert.IsTrue(FarmWorkDurations.GetTillDuration(level + 1) < FarmWorkDurations.GetTillDuration(level),
                    $"till duration must fall from level {level} to {level + 1}");
                Assert.IsTrue(FarmWorkDurations.GetHarvestDuration(level + 1) < FarmWorkDurations.GetHarvestDuration(level),
                    $"harvest duration must fall from level {level} to {level + 1}");
            }
        }

        [TestMethod]
        [TestCategory("Farming")]
        public void Durations_ClampOutsideTheSupportedLevelRange()
        {
            Assert.AreEqual(FarmWorkDurations.GetTillDuration(1), FarmWorkDurations.GetTillDuration(0), Tolerance,
                "levels below the minimum behave as level 1");
            Assert.AreEqual(FarmWorkDurations.GetTillDuration(1), FarmWorkDurations.GetTillDuration(-5), Tolerance,
                "negative levels behave as level 1");
            Assert.AreEqual(FarmWorkDurations.GetHarvestDuration(9), FarmWorkDurations.GetHarvestDuration(50), Tolerance,
                "levels above the maximum behave as level 9");
        }

        [TestMethod]
        [TestCategory("Farming")]
        public void AppleHarvestWait_ScalesWithTheHarvestCurve()
        {
            Assert.AreEqual(GameConfig.AppleHarvestWaitSeconds, FarmWorkDurations.GetAppleHarvestWaitDuration(1), Tolerance,
                "level 1 waits the configured apple-tree time");
            var expectedAtNine = GameConfig.AppleHarvestWaitSeconds
                * (GameConfig.HarvestDurationAtLevel9Seconds / GameConfig.HarvestDurationAtLevel1Seconds);
            Assert.AreEqual(expectedAtNine, FarmWorkDurations.GetAppleHarvestWaitDuration(9), Tolerance,
                "level 9 waits the same fraction as its harvest speed-up");
        }

        [TestMethod]
        [TestCategory("Farming")]
        public void SpeedScale_KeepsTheStepShapeForWaterAndPlant()
        {
            Assert.AreEqual(1f, FarmWorkDurations.GetSpeedScale(1), Tolerance, "level 1 is the unscaled baseline");
            Assert.AreEqual(1f - GameConfig.FarmProficiencySpeedStep * 8f, FarmWorkDurations.GetSpeedScale(9), Tolerance,
                "level 9 drops eight steps below the baseline");
        }
    }
}
