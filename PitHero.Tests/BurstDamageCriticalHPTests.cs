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
        // --- Strategic/Blitz tactic tests (default values) ---

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

        // --- Defensive tactic tests ---

        [TestMethod]
        public void BurstDamageThresholdPercentDefensive_Is15Percent()
        {
            Assert.AreEqual(0.15f, GameConfig.BurstDamageThresholdPercentDefensive);
        }

        [TestMethod]
        public void BurstDamageRecoveryPercentDefensive_Is80Percent()
        {
            Assert.AreEqual(0.80f, GameConfig.BurstDamageRecoveryPercentDefensive);
        }

        [TestMethod]
        public void BurstDamageThresholdPercentDefensive_IsLowerThanDefault()
        {
            // Defensive mode should trigger on smaller hits (more cautious)
            Assert.IsTrue(
                GameConfig.BurstDamageThresholdPercentDefensive < GameConfig.BurstDamageThresholdPercent,
                $"Defensive threshold ({GameConfig.BurstDamageThresholdPercentDefensive}) should be lower than default ({GameConfig.BurstDamageThresholdPercent})"
            );
        }

        [TestMethod]
        public void BurstDamageRecoveryPercentDefensive_IsHigherThanDefault()
        {
            // Defensive mode should require more HP to clear (more cautious)
            Assert.IsTrue(
                GameConfig.BurstDamageRecoveryPercentDefensive > GameConfig.BurstDamageRecoveryPercent,
                $"Defensive recovery ({GameConfig.BurstDamageRecoveryPercentDefensive}) should be higher than default ({GameConfig.BurstDamageRecoveryPercent})"
            );
        }

        [TestMethod]
        public void BurstDamageRecoveryPercentDefensive_IsGreaterThanCriticalHPPercent()
        {
            // Same invariant must hold for defensive mode
            Assert.IsTrue(
                GameConfig.BurstDamageRecoveryPercentDefensive > GameConfig.HeroCriticalHPPercent,
                $"BurstDamageRecoveryPercentDefensive ({GameConfig.BurstDamageRecoveryPercentDefensive}) must be greater than HeroCriticalHPPercent ({GameConfig.HeroCriticalHPPercent})"
            );
        }

        [TestMethod]
        public void BurstDamageThresholdPercentDefensive_IsPositiveAndLessThanOne()
        {
            Assert.IsTrue(GameConfig.BurstDamageThresholdPercentDefensive > 0f);
            Assert.IsTrue(GameConfig.BurstDamageThresholdPercentDefensive < 1.0f);
        }

        [TestMethod]
        public void BurstDamageRecoveryPercentDefensive_IsPositiveAndLessThanOne()
        {
            Assert.IsTrue(GameConfig.BurstDamageRecoveryPercentDefensive > 0f);
            Assert.IsTrue(GameConfig.BurstDamageRecoveryPercentDefensive < 1.0f);
        }

        [TestMethod]
        public void BurstDamageRecoveryPercentDefensive_IsGreaterThanThresholdPercentDefensive()
        {
            // Same invariant must hold for defensive mode
            Assert.IsTrue(
                GameConfig.BurstDamageRecoveryPercentDefensive > GameConfig.BurstDamageThresholdPercentDefensive,
                $"BurstDamageRecoveryPercentDefensive ({GameConfig.BurstDamageRecoveryPercentDefensive}) should be > BurstDamageThresholdPercentDefensive ({GameConfig.BurstDamageThresholdPercentDefensive})"
            );
        }
    }
}
