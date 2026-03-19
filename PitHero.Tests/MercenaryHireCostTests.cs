using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Balance;

namespace PitHero.Tests
{
    [TestClass]
    public class MercenaryHireCostTests
    {
        #region Hire Cost Formula Tests

        [TestMethod]
        public void CalculateMercenaryHireCost_Level1_Returns50()
        {
            var cost = BalanceConfig.CalculateMercenaryHireCost(1);
            Assert.AreEqual(50, cost, "Level 1 mercenary should cost 1 * 50 = 50 gold");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_Level9_Returns450()
        {
            var cost = BalanceConfig.CalculateMercenaryHireCost(9);
            Assert.AreEqual(450, cost, "Level 9 mercenary should cost 9 * 50 = 450 gold");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_Level10_Returns750()
        {
            // Level 10: bracket 1, multiplier = 50 + 1*25 = 75
            var cost = BalanceConfig.CalculateMercenaryHireCost(10);
            Assert.AreEqual(750, cost, "Level 10 mercenary should cost 10 * 75 = 750 gold");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_Level19_Returns1425()
        {
            var cost = BalanceConfig.CalculateMercenaryHireCost(19);
            Assert.AreEqual(1425, cost, "Level 19 mercenary should cost 19 * 75 = 1425 gold");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_Level20_Returns2000()
        {
            // Level 20: bracket 2, multiplier = 50 + 2*25 = 100
            var cost = BalanceConfig.CalculateMercenaryHireCost(20);
            Assert.AreEqual(2000, cost, "Level 20 mercenary should cost 20 * 100 = 2000 gold");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_Level30_Returns3750()
        {
            // Level 30: bracket 3, multiplier = 50 + 3*25 = 125
            var cost = BalanceConfig.CalculateMercenaryHireCost(30);
            Assert.AreEqual(3750, cost, "Level 30 mercenary should cost 30 * 125 = 3750 gold");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_Level99_Returns27225()
        {
            // Level 99: bracket 9, multiplier = 50 + 9*25 = 275
            var cost = BalanceConfig.CalculateMercenaryHireCost(99);
            Assert.AreEqual(27225, cost, "Level 99 mercenary should cost 99 * 275 = 27225 gold");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_BelowLevel1_ClampsTo1()
        {
            var cost = BalanceConfig.CalculateMercenaryHireCost(0);
            Assert.AreEqual(50, cost, "Level 0 should clamp to level 1 (50 gold)");

            cost = BalanceConfig.CalculateMercenaryHireCost(-5);
            Assert.AreEqual(50, cost, "Negative level should clamp to level 1 (50 gold)");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_AboveMaxLevel_ClampsTo99()
        {
            var cost = BalanceConfig.CalculateMercenaryHireCost(100);
            Assert.AreEqual(27225, cost, "Level 100 should clamp to level 99 (27225 gold)");
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_CostIncreasesWithLevel()
        {
            var previousCost = 0;
            for (int level = 1; level <= 99; level++)
            {
                var cost = BalanceConfig.CalculateMercenaryHireCost(level);
                Assert.IsTrue(cost > previousCost,
                    $"Cost at level {level} ({cost}) should be greater than cost at level {level - 1} ({previousCost})");
                previousCost = cost;
            }
        }

        [TestMethod]
        public void CalculateMercenaryHireCost_MultiplierIncreasesEvery10Levels()
        {
            // Verify cost-per-level increases at bracket boundaries
            // Level 9 (bracket 0): 50/level -> 9*50 = 450
            // Level 10 (bracket 1): 75/level -> 10*75 = 750
            Assert.AreEqual(450, BalanceConfig.CalculateMercenaryHireCost(9));
            Assert.AreEqual(750, BalanceConfig.CalculateMercenaryHireCost(10));

            // Level 19 (bracket 1): 75/level -> 19*75 = 1425
            // Level 20 (bracket 2): 100/level -> 20*100 = 2000
            Assert.AreEqual(1425, BalanceConfig.CalculateMercenaryHireCost(19));
            Assert.AreEqual(2000, BalanceConfig.CalculateMercenaryHireCost(20));
        }

        #endregion

        #region Level Distribution Tests

        [TestMethod]
        public void DetermineMercenaryLevel_HeroLevel1_AlwaysReturnsAtLeast1()
        {
            for (int i = 0; i < 100; i++)
            {
                var level = MercenaryManager.DetermineMercenaryLevel(1);
                Assert.IsTrue(level >= 1, $"Mercenary level should always be at least 1, got {level}");
            }
        }

        [TestMethod]
        public void DetermineMercenaryLevel_HeroLevel50_ReturnsWithinRange()
        {
            for (int i = 0; i < 100; i++)
            {
                var level = MercenaryManager.DetermineMercenaryLevel(50);
                Assert.IsTrue(level >= 1, $"Mercenary level should be at least 1, got {level}");
                Assert.IsTrue(level <= 50, $"Mercenary level should not exceed hero level 50, got {level}");
            }
        }

        [TestMethod]
        public void DetermineMercenaryLevel_HeroLevel0_ClampsToAtLeast1()
        {
            for (int i = 0; i < 50; i++)
            {
                var level = MercenaryManager.DetermineMercenaryLevel(0);
                Assert.IsTrue(level >= 1, $"Mercenary level should always be at least 1 even with heroLevel 0, got {level}");
            }
        }

        [TestMethod]
        public void DetermineMercenaryLevel_NegativeHeroLevel_ClampsToAtLeast1()
        {
            for (int i = 0; i < 50; i++)
            {
                var level = MercenaryManager.DetermineMercenaryLevel(-5);
                Assert.IsTrue(level >= 1, $"Mercenary level should always be at least 1 even with negative heroLevel, got {level}");
            }
        }

        [TestMethod]
        public void DetermineMercenaryLevel_ProducesVariedLevels()
        {
            // With hero level 50, we should see a variety of levels over many rolls
            var seenLevel1 = false;
            var seenLowLevel = false;
            var seenMidLevel = false;
            var seenHighLevel = false;
            var seenHeroLevel = false;

            for (int i = 0; i < 1000; i++)
            {
                var level = MercenaryManager.DetermineMercenaryLevel(50);
                if (level == 1) seenLevel1 = true;
                else if (level <= 16) seenLowLevel = true;
                else if (level <= 25) seenMidLevel = true;
                else if (level < 50) seenHighLevel = true;
                else if (level == 50) seenHeroLevel = true;
            }

            Assert.IsTrue(seenLevel1, "Should sometimes produce level 1 (20% chance)");
            Assert.IsTrue(seenLowLevel, "Should sometimes produce low levels (30% chance for 1 to heroLevel/3)");
            Assert.IsTrue(seenHeroLevel, "Should sometimes produce hero level (10% chance)");
        }

        #endregion
    }
}
