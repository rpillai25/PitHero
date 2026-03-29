using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for burst damage CriticalHP detection feature.
    /// These tests cover the GameConfig constants and their relationships.
    /// Full integration tests for HeroComponent require the Nez ECS context.
    /// </summary>
    [TestClass]
    public class BurstDamageCriticalHPTests
    {
        [TestMethod]
        public void BurstDamageThresholdPercent_DefaultIs20Percent()
        {
            Assert.AreEqual(0.20f, GameConfig.BurstDamageThresholdPercent);
        }

        [TestMethod]
        public void BurstDamageRecoveryPercent_DefaultIs60Percent()
        {
            Assert.AreEqual(0.60f, GameConfig.BurstDamageRecoveryPercent);
        }

        [TestMethod]
        public void BurstDamageRecoveryPercent_IsGreaterThanCriticalHPPercent()
        {
            // Recovery threshold must be above the normal critical threshold, otherwise
            // the burst flag would never clear (the normal critical check would keep triggering first)
            Assert.IsTrue(
                GameConfig.BurstDamageRecoveryPercent > GameConfig.HeroCriticalHPPercent,
                $"BurstDamageRecoveryPercent ({GameConfig.BurstDamageRecoveryPercent}) must be greater than HeroCriticalHPPercent ({GameConfig.HeroCriticalHPPercent})"
            );
        }

        [TestMethod]
        public void BurstDamageThresholdPercent_IsPositiveAndLessThanOne()
        {
            Assert.IsTrue(GameConfig.BurstDamageThresholdPercent > 0f);
            Assert.IsTrue(GameConfig.BurstDamageThresholdPercent < 1.0f);
        }

        [TestMethod]
        public void BurstDamageRecoveryPercent_IsPositiveAndLessThanOne()
        {
            Assert.IsTrue(GameConfig.BurstDamageRecoveryPercent > 0f);
            Assert.IsTrue(GameConfig.BurstDamageRecoveryPercent < 1.0f);
        }

        [TestMethod]
        public void BurstDamageRecoveryPercent_IsGreaterThanThresholdPercent()
        {
            // Sanity check: recovery threshold should be higher than burst trigger threshold
            // This ensures that triggering burst (from high damage) requires more HP to recover
            Assert.IsTrue(
                GameConfig.BurstDamageRecoveryPercent > GameConfig.BurstDamageThresholdPercent,
                $"BurstDamageRecoveryPercent ({GameConfig.BurstDamageRecoveryPercent}) should be > BurstDamageThresholdPercent ({GameConfig.BurstDamageThresholdPercent})"
            );
        }
    }
}
