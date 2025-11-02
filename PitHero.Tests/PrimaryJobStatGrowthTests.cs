using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for primary job stat growth curves to ensure they reach target values at various levels.
    /// Tests validate that stats stay within caps and reach targets with ±5% tolerance at level 99.
    /// </summary>
    [TestClass]
    public class PrimaryJobStatGrowthTests
    {
        private const double TolerancePercent = 0.05; // 5% tolerance

        #region Knight Tests

        [TestMethod]
        public void Knight_Level1_HasCorrectBaseStats()
        {
            var knight = new Knight();
            var stats = knight.BaseBonus;

            Assert.AreEqual(8, stats.Strength, "Knight base Strength should be 8");
            Assert.AreEqual(5, stats.Agility, "Knight base Agility should be 5");
            Assert.AreEqual(9, stats.Vitality, "Knight base Vitality should be 9");
            Assert.AreEqual(3, stats.Magic, "Knight base Magic should be 3");
        }

        [TestMethod]
        public void Knight_Level25_StatsWithinExpectedRange()
        {
            var knight = new Knight();
            var stats = knight.GetJobContributionAtLevel(25);

            // At level 25: base + (growth * 24)
            // Str: 8 + (0.612 * 24) ≈ 22.7
            // Agi: 5 + (0.378 * 24) ≈ 14.1
            // Vit: 9 + (0.704 * 24) ≈ 25.9
            // Mag: 3 + (0.255 * 24) ≈ 9.1

            Assert.IsTrue(stats.Strength >= 20 && stats.Strength <= 25, $"Knight Str at L25 should be ~23, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 12 && stats.Agility <= 16, $"Knight Agi at L25 should be ~14, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 24 && stats.Vitality <= 28, $"Knight Vit at L25 should be ~26, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 8 && stats.Magic <= 11, $"Knight Mag at L25 should be ~9, was {stats.Magic}");
        }

        [TestMethod]
        public void Knight_Level50_StatsWithinExpectedRange()
        {
            var knight = new Knight();
            var stats = knight.GetJobContributionAtLevel(50);

            // At level 50: base + (growth * 49)
            // Str: 8 + (0.612 * 49) ≈ 38.0
            // Agi: 5 + (0.378 * 49) ≈ 23.5
            // Vit: 9 + (0.704 * 49) ≈ 43.5
            // Mag: 3 + (0.255 * 49) ≈ 15.5

            Assert.IsTrue(stats.Strength >= 36 && stats.Strength <= 40, $"Knight Str at L50 should be ~38, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 22 && stats.Agility <= 25, $"Knight Agi at L50 should be ~24, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 42 && stats.Vitality <= 45, $"Knight Vit at L50 should be ~44, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 14 && stats.Magic <= 17, $"Knight Mag at L50 should be ~16, was {stats.Magic}");
        }

        [TestMethod]
        public void Knight_Level75_StatsWithinExpectedRange()
        {
            var knight = new Knight();
            var stats = knight.GetJobContributionAtLevel(75);

            // At level 75: base + (growth * 74)
            // Str: 8 + (0.612 * 74) ≈ 53.3
            // Agi: 5 + (0.378 * 74) ≈ 33.0
            // Vit: 9 + (0.704 * 74) ≈ 61.1
            // Mag: 3 + (0.255 * 74) ≈ 21.9

            Assert.IsTrue(stats.Strength >= 51 && stats.Strength <= 55, $"Knight Str at L75 should be ~53, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 31 && stats.Agility <= 35, $"Knight Agi at L75 should be ~33, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 59 && stats.Vitality <= 63, $"Knight Vit at L75 should be ~61, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 20 && stats.Magic <= 24, $"Knight Mag at L75 should be ~22, was {stats.Magic}");
        }

        [TestMethod]
        public void Knight_Level99_ReachesTargetStats()
        {
            var knight = new Knight();
            var stats = knight.GetJobContributionAtLevel(StatConstants.MaxLevel);

            // Target stats at L99
            const int targetStr = 68;
            const int targetAgi = 42;
            const int targetVit = 78;
            const int targetMag = 28;

            AssertWithinTolerance(stats.Strength, targetStr, "Knight Strength at L99");
            AssertWithinTolerance(stats.Agility, targetAgi, "Knight Agility at L99");
            AssertWithinTolerance(stats.Vitality, targetVit, "Knight Vitality at L99");
            AssertWithinTolerance(stats.Magic, targetMag, "Knight Magic at L99");

            // Verify stats don't exceed caps
            Assert.IsTrue(stats.Strength <= StatConstants.MaxStat, "Strength should not exceed cap");
            Assert.IsTrue(stats.Agility <= StatConstants.MaxStat, "Agility should not exceed cap");
            Assert.IsTrue(stats.Vitality <= StatConstants.MaxStat, "Vitality should not exceed cap");
            Assert.IsTrue(stats.Magic <= StatConstants.MaxStat, "Magic should not exceed cap");
        }

        #endregion

        #region Monk Tests

        [TestMethod]
        public void Monk_Level1_HasCorrectBaseStats()
        {
            var monk = new Monk();
            var stats = monk.BaseBonus;

            Assert.AreEqual(9, stats.Strength, "Monk base Strength should be 9");
            Assert.AreEqual(7, stats.Agility, "Monk base Agility should be 7");
            Assert.AreEqual(7, stats.Vitality, "Monk base Vitality should be 7");
            Assert.AreEqual(4, stats.Magic, "Monk base Magic should be 4");
        }

        [TestMethod]
        public void Monk_Level25_StatsWithinExpectedRange()
        {
            var monk = new Monk();
            var stats = monk.GetJobContributionAtLevel(25);

            Assert.IsTrue(stats.Strength >= 23 && stats.Strength <= 27, $"Monk Str at L25, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 18 && stats.Agility <= 22, $"Monk Agi at L25, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 17 && stats.Vitality <= 21, $"Monk Vit at L25, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 10 && stats.Magic <= 14, $"Monk Mag at L25, was {stats.Magic}");
        }

        [TestMethod]
        public void Monk_Level50_StatsWithinExpectedRange()
        {
            var monk = new Monk();
            var stats = monk.GetJobContributionAtLevel(50);

            Assert.IsTrue(stats.Strength >= 39 && stats.Strength <= 43, $"Monk Str at L50, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 33 && stats.Agility <= 37, $"Monk Agi at L50, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 30 && stats.Vitality <= 34, $"Monk Vit at L50, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 18 && stats.Magic <= 22, $"Monk Mag at L50, was {stats.Magic}");
        }

        [TestMethod]
        public void Monk_Level75_StatsWithinExpectedRange()
        {
            var monk = new Monk();
            var stats = monk.GetJobContributionAtLevel(75);

            Assert.IsTrue(stats.Strength >= 55 && stats.Strength <= 59, $"Monk Str at L75, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 47 && stats.Agility <= 51, $"Monk Agi at L75, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 43 && stats.Vitality <= 47, $"Monk Vit at L75, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 27 && stats.Magic <= 31, $"Monk Mag at L75, was {stats.Magic}");
        }

        [TestMethod]
        public void Monk_Level99_ReachesTargetStats()
        {
            var monk = new Monk();
            var stats = monk.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 73, "Monk Strength at L99");
            AssertWithinTolerance(stats.Agility, 62, "Monk Agility at L99");
            AssertWithinTolerance(stats.Vitality, 58, "Monk Vitality at L99");
            AssertWithinTolerance(stats.Magic, 37, "Monk Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Thief Tests

        [TestMethod]
        public void Thief_Level1_HasCorrectBaseStats()
        {
            var thief = new Thief();
            var stats = thief.BaseBonus;

            Assert.AreEqual(7, stats.Strength, "Thief base Strength should be 7");
            Assert.AreEqual(10, stats.Agility, "Thief base Agility should be 10");
            Assert.AreEqual(5, stats.Vitality, "Thief base Vitality should be 5");
            Assert.AreEqual(4, stats.Magic, "Thief base Magic should be 4");
        }

        [TestMethod]
        public void Thief_Level25_StatsWithinExpectedRange()
        {
            var thief = new Thief();
            var stats = thief.GetJobContributionAtLevel(25);

            Assert.IsTrue(stats.Strength >= 17 && stats.Strength <= 21, $"Thief Str at L25, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 26 && stats.Agility <= 30, $"Thief Agi at L25, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 12 && stats.Vitality <= 16, $"Thief Vit at L25, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 10 && stats.Magic <= 14, $"Thief Mag at L25, was {stats.Magic}");
        }

        [TestMethod]
        public void Thief_Level50_StatsWithinExpectedRange()
        {
            var thief = new Thief();
            var stats = thief.GetJobContributionAtLevel(50);

            Assert.IsTrue(stats.Strength >= 30 && stats.Strength <= 34, $"Thief Str at L50, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 44 && stats.Agility <= 48, $"Thief Agi at L50, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 22 && stats.Vitality <= 26, $"Thief Vit at L50, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 16 && stats.Magic <= 20, $"Thief Mag at L50, was {stats.Magic}");
        }

        [TestMethod]
        public void Thief_Level75_StatsWithinExpectedRange()
        {
            var thief = new Thief();
            var stats = thief.GetJobContributionAtLevel(75);

            Assert.IsTrue(stats.Strength >= 43 && stats.Strength <= 47, $"Thief Str at L75, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 62 && stats.Agility <= 66, $"Thief Agi at L75, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 32 && stats.Vitality <= 36, $"Thief Vit at L75, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 23 && stats.Magic <= 27, $"Thief Mag at L75, was {stats.Magic}");
        }

        [TestMethod]
        public void Thief_Level99_ReachesTargetStats()
        {
            var thief = new Thief();
            var stats = thief.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 58, "Thief Strength at L99");
            AssertWithinTolerance(stats.Agility, 82, "Thief Agility at L99");
            AssertWithinTolerance(stats.Vitality, 43, "Thief Vitality at L99");
            AssertWithinTolerance(stats.Magic, 32, "Thief Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Bowman Tests

        [TestMethod]
        public void Bowman_Level1_HasCorrectBaseStats()
        {
            var bowman = new Bowman();
            var stats = bowman.BaseBonus;

            Assert.AreEqual(7, stats.Strength, "Bowman base Strength should be 7");
            Assert.AreEqual(9, stats.Agility, "Bowman base Agility should be 9");
            Assert.AreEqual(6, stats.Vitality, "Bowman base Vitality should be 6");
            Assert.AreEqual(4, stats.Magic, "Bowman base Magic should be 4");
        }

        [TestMethod]
        public void Bowman_Level25_StatsWithinExpectedRange()
        {
            var bowman = new Bowman();
            var stats = bowman.GetJobContributionAtLevel(25);

            Assert.IsTrue(stats.Strength >= 18 && stats.Strength <= 22, $"Bowman Str at L25, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 22 && stats.Agility <= 26, $"Bowman Agi at L25, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 14 && stats.Vitality <= 18, $"Bowman Vit at L25, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 10 && stats.Magic <= 14, $"Bowman Mag at L25, was {stats.Magic}");
        }

        [TestMethod]
        public void Bowman_Level50_StatsWithinExpectedRange()
        {
            var bowman = new Bowman();
            var stats = bowman.GetJobContributionAtLevel(50);

            Assert.IsTrue(stats.Strength >= 32 && stats.Strength <= 36, $"Bowman Str at L50, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 38 && stats.Agility <= 42, $"Bowman Agi at L50, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 24 && stats.Vitality <= 28, $"Bowman Vit at L50, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 18 && stats.Magic <= 22, $"Bowman Mag at L50, was {stats.Magic}");
        }

        [TestMethod]
        public void Bowman_Level75_StatsWithinExpectedRange()
        {
            var bowman = new Bowman();
            var stats = bowman.GetJobContributionAtLevel(75);

            Assert.IsTrue(stats.Strength >= 46 && stats.Strength <= 50, $"Bowman Str at L75, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 54 && stats.Agility <= 58, $"Bowman Agi at L75, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 35 && stats.Vitality <= 39, $"Bowman Vit at L75, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 27 && stats.Magic <= 31, $"Bowman Mag at L75, was {stats.Magic}");
        }

        [TestMethod]
        public void Bowman_Level99_ReachesTargetStats()
        {
            var bowman = new Bowman();
            var stats = bowman.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 62, "Bowman Strength at L99");
            AssertWithinTolerance(stats.Agility, 72, "Bowman Agility at L99");
            AssertWithinTolerance(stats.Vitality, 48, "Bowman Vitality at L99");
            AssertWithinTolerance(stats.Magic, 37, "Bowman Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Mage Tests

        [TestMethod]
        public void Mage_Level1_HasCorrectBaseStats()
        {
            var mage = new Mage();
            var stats = mage.BaseBonus;

            Assert.AreEqual(4, stats.Strength, "Mage base Strength should be 4");
            Assert.AreEqual(6, stats.Agility, "Mage base Agility should be 6");
            Assert.AreEqual(4, stats.Vitality, "Mage base Vitality should be 4");
            Assert.AreEqual(11, stats.Magic, "Mage base Magic should be 11");
        }

        [TestMethod]
        public void Mage_Level25_StatsWithinExpectedRange()
        {
            var mage = new Mage();
            var stats = mage.GetJobContributionAtLevel(25);

            Assert.IsTrue(stats.Strength >= 9 && stats.Strength <= 13, $"Mage Str at L25, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 14 && stats.Agility <= 18, $"Mage Agi at L25, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 9 && stats.Vitality <= 13, $"Mage Vit at L25, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 27 && stats.Magic <= 31, $"Mage Mag at L25, was {stats.Magic}");
        }

        [TestMethod]
        public void Mage_Level50_StatsWithinExpectedRange()
        {
            var mage = new Mage();
            var stats = mage.GetJobContributionAtLevel(50);

            Assert.IsTrue(stats.Strength >= 16 && stats.Strength <= 20, $"Mage Str at L50, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 25 && stats.Agility <= 29, $"Mage Agi at L50, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 16 && stats.Vitality <= 20, $"Mage Vit at L50, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 46 && stats.Magic <= 50, $"Mage Mag at L50, was {stats.Magic}");
        }

        [TestMethod]
        public void Mage_Level75_StatsWithinExpectedRange()
        {
            var mage = new Mage();
            var stats = mage.GetJobContributionAtLevel(75);

            Assert.IsTrue(stats.Strength >= 23 && stats.Strength <= 27, $"Mage Str at L75, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 36 && stats.Agility <= 40, $"Mage Agi at L75, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 23 && stats.Vitality <= 27, $"Mage Vit at L75, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 66 && stats.Magic <= 70, $"Mage Mag at L75, was {stats.Magic}");
        }

        [TestMethod]
        public void Mage_Level99_ReachesTargetStats()
        {
            var mage = new Mage();
            var stats = mage.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 33, "Mage Strength at L99");
            AssertWithinTolerance(stats.Agility, 48, "Mage Agility at L99");
            AssertWithinTolerance(stats.Vitality, 33, "Mage Vitality at L99");
            AssertWithinTolerance(stats.Magic, 88, "Mage Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Priest Tests

        [TestMethod]
        public void Priest_Level1_HasCorrectBaseStats()
        {
            var priest = new Priest();
            var stats = priest.BaseBonus;

            Assert.AreEqual(5, stats.Strength, "Priest base Strength should be 5");
            Assert.AreEqual(6, stats.Agility, "Priest base Agility should be 6");
            Assert.AreEqual(5, stats.Vitality, "Priest base Vitality should be 5");
            Assert.AreEqual(9, stats.Magic, "Priest base Magic should be 9");
        }

        [TestMethod]
        public void Priest_Level25_StatsWithinExpectedRange()
        {
            var priest = new Priest();
            var stats = priest.GetJobContributionAtLevel(25);

            Assert.IsTrue(stats.Strength >= 11 && stats.Strength <= 15, $"Priest Str at L25, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 16 && stats.Agility <= 20, $"Priest Agi at L25, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 12 && stats.Vitality <= 16, $"Priest Vit at L25, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 24 && stats.Magic <= 28, $"Priest Mag at L25, was {stats.Magic}");
        }

        [TestMethod]
        public void Priest_Level50_StatsWithinExpectedRange()
        {
            var priest = new Priest();
            var stats = priest.GetJobContributionAtLevel(50);

            Assert.IsTrue(stats.Strength >= 19 && stats.Strength <= 23, $"Priest Str at L50, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 27 && stats.Agility <= 31, $"Priest Agi at L50, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 21 && stats.Vitality <= 25, $"Priest Vit at L50, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 42 && stats.Magic <= 46, $"Priest Mag at L50, was {stats.Magic}");
        }

        [TestMethod]
        public void Priest_Level75_StatsWithinExpectedRange()
        {
            var priest = new Priest();
            var stats = priest.GetJobContributionAtLevel(75);

            Assert.IsTrue(stats.Strength >= 27 && stats.Strength <= 31, $"Priest Str at L75, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 39 && stats.Agility <= 43, $"Priest Agi at L75, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 31 && stats.Vitality <= 35, $"Priest Vit at L75, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 59 && stats.Magic <= 63, $"Priest Mag at L75, was {stats.Magic}");
        }

        [TestMethod]
        public void Priest_Level99_ReachesTargetStats()
        {
            var priest = new Priest();
            var stats = priest.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 38, "Priest Strength at L99");
            AssertWithinTolerance(stats.Agility, 53, "Priest Agility at L99");
            AssertWithinTolerance(stats.Vitality, 43, "Priest Vitality at L99");
            AssertWithinTolerance(stats.Magic, 78, "Priest Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Helper Methods

        /// <summary>Asserts that a value is within ±5% tolerance of the target.</summary>
        private void AssertWithinTolerance(int actual, int target, string statName)
        {
            double diff = Math.Abs(actual - target);
            double percentDiff = diff / target;

            Assert.IsTrue(percentDiff <= TolerancePercent,
                $"{statName}: Expected {target} ±5%, got {actual} (diff: {diff}, {percentDiff * 100:F2}%)");
        }

        /// <summary>Asserts that all stats are within their maximum caps.</summary>
        private void AssertWithinCaps(StatBlock stats)
        {
            Assert.IsTrue(stats.Strength <= StatConstants.MaxStat, "Strength should not exceed cap");
            Assert.IsTrue(stats.Agility <= StatConstants.MaxStat, "Agility should not exceed cap");
            Assert.IsTrue(stats.Vitality <= StatConstants.MaxStat, "Vitality should not exceed cap");
            Assert.IsTrue(stats.Magic <= StatConstants.MaxStat, "Magic should not exceed cap");
        }

        #endregion
    }
}
