using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Jobs.Secondary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for secondary job stat growth curves to ensure they reach target values at various levels.
    /// Tests validate that stats stay within caps and reach targets with ±5% tolerance at level 99.
    /// Secondary jobs should be 15-25% stronger than their parent primary jobs.
    /// </summary>
    [TestClass]
    public class SecondaryJobStatGrowthTests
    {
        private const double TolerancePercent = 0.05; // 5% tolerance

        #region ArcaneArcher Tests

        [TestMethod]
        public void ArcaneArcher_Level1_HasCorrectBaseStats()
        {
            var job = new ArcaneArcher();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "ArcaneArcher base Strength should be 2");
            Assert.AreEqual(2, stats.Agility, "ArcaneArcher base Agility should be 2");
            Assert.AreEqual(2, stats.Vitality, "ArcaneArcher base Vitality should be 2");
            Assert.AreEqual(4, stats.Magic, "ArcaneArcher base Magic should be 4");
        }

        [TestMethod]
        public void ArcaneArcher_Level25_StatsWithinExpectedRange()
        {
            var job = new ArcaneArcher();
            var stats = job.GetJobContributionAtLevel(25);

            Assert.IsTrue(stats.Strength >= 15 && stats.Strength <= 19, $"ArcaneArcher Str at L25, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 20 && stats.Agility <= 24, $"ArcaneArcher Agi at L25, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 12 && stats.Vitality <= 16, $"ArcaneArcher Vit at L25, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 24 && stats.Magic <= 28, $"ArcaneArcher Mag at L25, was {stats.Magic}");
        }

        [TestMethod]
        public void ArcaneArcher_Level50_StatsWithinExpectedRange()
        {
            var job = new ArcaneArcher();
            var stats = job.GetJobContributionAtLevel(50);

            Assert.IsTrue(stats.Strength >= 32 && stats.Strength <= 36, $"ArcaneArcher Str at L50, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 42 && stats.Agility <= 46, $"ArcaneArcher Agi at L50, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 27 && stats.Vitality <= 31, $"ArcaneArcher Vit at L50, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 46 && stats.Magic <= 50, $"ArcaneArcher Mag at L50, was {stats.Magic}");
        }

        [TestMethod]
        public void ArcaneArcher_Level75_StatsWithinExpectedRange()
        {
            var job = new ArcaneArcher();
            var stats = job.GetJobContributionAtLevel(75);

            Assert.IsTrue(stats.Strength >= 48 && stats.Strength <= 52, $"ArcaneArcher Str at L75, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 64 && stats.Agility <= 68, $"ArcaneArcher Agi at L75, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 41 && stats.Vitality <= 45, $"ArcaneArcher Vit at L75, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 68 && stats.Magic <= 72, $"ArcaneArcher Mag at L75, was {stats.Magic}");
        }

        [TestMethod]
        public void ArcaneArcher_Level99_ReachesTargetStats()
        {
            var job = new ArcaneArcher();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 65, "ArcaneArcher Strength at L99");
            AssertWithinTolerance(stats.Agility, 85, "ArcaneArcher Agility at L99");
            AssertWithinTolerance(stats.Vitality, 55, "ArcaneArcher Vitality at L99");
            AssertWithinTolerance(stats.Magic, 92, "ArcaneArcher Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region DivineFist Tests

        [TestMethod]
        public void DivineFist_Level1_HasCorrectBaseStats()
        {
            var job = new DivineFist();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength, "DivineFist base Strength should be 3");
            Assert.AreEqual(2, stats.Agility, "DivineFist base Agility should be 2");
            Assert.AreEqual(2, stats.Vitality, "DivineFist base Vitality should be 2");
            Assert.AreEqual(3, stats.Magic, "DivineFist base Magic should be 3");
        }

        [TestMethod]
        public void DivineFist_Level25_StatsWithinExpectedRange()
        {
            var job = new DivineFist();
            var stats = job.GetJobContributionAtLevel(25);

            Assert.IsTrue(stats.Strength >= 18 && stats.Strength <= 22, $"DivineFist Str at L25, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 20 && stats.Agility <= 24, $"DivineFist Agi at L25, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 16 && stats.Vitality <= 20, $"DivineFist Vit at L25, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 22 && stats.Magic <= 26, $"DivineFist Mag at L25, was {stats.Magic}");
        }

        [TestMethod]
        public void DivineFist_Level50_StatsWithinExpectedRange()
        {
            var job = new DivineFist();
            var stats = job.GetJobContributionAtLevel(50);

            Assert.IsTrue(stats.Strength >= 36 && stats.Strength <= 40, $"DivineFist Str at L50, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 39 && stats.Agility <= 43, $"DivineFist Agi at L50, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 31 && stats.Vitality <= 35, $"DivineFist Vit at L50, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 43 && stats.Magic <= 47, $"DivineFist Mag at L50, was {stats.Magic}");
        }

        [TestMethod]
        public void DivineFist_Level75_StatsWithinExpectedRange()
        {
            var job = new DivineFist();
            var stats = job.GetJobContributionAtLevel(75);

            Assert.IsTrue(stats.Strength >= 54 && stats.Strength <= 58, $"DivineFist Str at L75, was {stats.Strength}");
            Assert.IsTrue(stats.Agility >= 59 && stats.Agility <= 63, $"DivineFist Agi at L75, was {stats.Agility}");
            Assert.IsTrue(stats.Vitality >= 47 && stats.Vitality <= 51, $"DivineFist Vit at L75, was {stats.Vitality}");
            Assert.IsTrue(stats.Magic >= 64 && stats.Magic <= 68, $"DivineFist Mag at L75, was {stats.Magic}");
        }

        [TestMethod]
        public void DivineFist_Level99_ReachesTargetStats()
        {
            var job = new DivineFist();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 72, "DivineFist Strength at L99");
            AssertWithinTolerance(stats.Agility, 78, "DivineFist Agility at L99");
            AssertWithinTolerance(stats.Vitality, 62, "DivineFist Vitality at L99");
            AssertWithinTolerance(stats.Magic, 85, "DivineFist Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region DragonFist Tests

        [TestMethod]
        public void DragonFist_Level1_HasCorrectBaseStats()
        {
            var job = new DragonFist();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength, "DragonFist base Strength should be 3");
            Assert.AreEqual(3, stats.Agility, "DragonFist base Agility should be 3");
            Assert.AreEqual(2, stats.Vitality, "DragonFist base Vitality should be 2");
            Assert.AreEqual(2, stats.Magic, "DragonFist base Magic should be 2");
        }

        [TestMethod]
        public void DragonFist_Level99_ReachesTargetStats()
        {
            var job = new DragonFist();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 77, "DragonFist Strength at L99");
            AssertWithinTolerance(stats.Agility, 80, "DragonFist Agility at L99");
            AssertWithinTolerance(stats.Vitality, 60, "DragonFist Vitality at L99");
            AssertWithinTolerance(stats.Magic, 75, "DragonFist Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region HolyArcher Tests

        [TestMethod]
        public void HolyArcher_Level1_HasCorrectBaseStats()
        {
            var job = new HolyArcher();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "HolyArcher base Strength should be 2");
            Assert.AreEqual(2, stats.Agility, "HolyArcher base Agility should be 2");
            Assert.AreEqual(2, stats.Vitality, "HolyArcher base Vitality should be 2");
            Assert.AreEqual(3, stats.Magic, "HolyArcher base Magic should be 3");
        }

        [TestMethod]
        public void HolyArcher_Level99_ReachesTargetStats()
        {
            var job = new HolyArcher();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 68, "HolyArcher Strength at L99");
            AssertWithinTolerance(stats.Agility, 82, "HolyArcher Agility at L99");
            AssertWithinTolerance(stats.Vitality, 60, "HolyArcher Vitality at L99");
            AssertWithinTolerance(stats.Magic, 88, "HolyArcher Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region KiShot Tests

        [TestMethod]
        public void KiShot_Level1_HasCorrectBaseStats()
        {
            var job = new KiShot();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "KiShot base Strength should be 2");
            Assert.AreEqual(3, stats.Agility, "KiShot base Agility should be 3");
            Assert.AreEqual(2, stats.Vitality, "KiShot base Vitality should be 2");
            Assert.AreEqual(2, stats.Magic, "KiShot base Magic should be 2");
        }

        [TestMethod]
        public void KiShot_Level99_ReachesTargetStats()
        {
            var job = new KiShot();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 70, "KiShot Strength at L99");
            AssertWithinTolerance(stats.Agility, 90, "KiShot Agility at L99");
            AssertWithinTolerance(stats.Vitality, 62, "KiShot Vitality at L99");
            AssertWithinTolerance(stats.Magic, 70, "KiShot Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Marksman Tests

        [TestMethod]
        public void Marksman_Level1_HasCorrectBaseStats()
        {
            var job = new Marksman();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "Marksman base Strength should be 2");
            Assert.AreEqual(3, stats.Agility, "Marksman base Agility should be 3");
            Assert.AreEqual(2, stats.Vitality, "Marksman base Vitality should be 2");
            Assert.AreEqual(1, stats.Magic, "Marksman base Magic should be 1");
        }

        [TestMethod]
        public void Marksman_Level99_ReachesTargetStats()
        {
            var job = new Marksman();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 68, "Marksman Strength at L99");
            AssertWithinTolerance(stats.Agility, 88, "Marksman Agility at L99");
            AssertWithinTolerance(stats.Vitality, 58, "Marksman Vitality at L99");
            AssertWithinTolerance(stats.Magic, 65, "Marksman Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Ninja Tests

        [TestMethod]
        public void Ninja_Level1_HasCorrectBaseStats()
        {
            var job = new Ninja();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength, "Ninja base Strength should be 3");
            Assert.AreEqual(3, stats.Agility, "Ninja base Agility should be 3");
            Assert.AreEqual(2, stats.Vitality, "Ninja base Vitality should be 2");
            Assert.AreEqual(1, stats.Magic, "Ninja base Magic should be 1");
        }

        [TestMethod]
        public void Ninja_Level99_ReachesTargetStats()
        {
            var job = new Ninja();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 72, "Ninja Strength at L99");
            AssertWithinTolerance(stats.Agility, 92, "Ninja Agility at L99");
            AssertWithinTolerance(stats.Vitality, 60, "Ninja Vitality at L99");
            AssertWithinTolerance(stats.Magic, 62, "Ninja Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Paladin Tests

        [TestMethod]
        public void Paladin_Level1_HasCorrectBaseStats()
        {
            var job = new Paladin();
            var stats = job.BaseBonus;

            Assert.AreEqual(4, stats.Strength, "Paladin base Strength should be 4");
            Assert.AreEqual(1, stats.Agility, "Paladin base Agility should be 1");
            Assert.AreEqual(3, stats.Vitality, "Paladin base Vitality should be 3");
            Assert.AreEqual(2, stats.Magic, "Paladin base Magic should be 2");
        }

        [TestMethod]
        public void Paladin_Level99_ReachesTargetStats()
        {
            var job = new Paladin();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 75, "Paladin Strength at L99");
            AssertWithinTolerance(stats.Agility, 48, "Paladin Agility at L99");
            AssertWithinTolerance(stats.Vitality, 85, "Paladin Vitality at L99");
            AssertWithinTolerance(stats.Magic, 60, "Paladin Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Samurai Tests

        [TestMethod]
        public void Samurai_Level1_HasCorrectBaseStats()
        {
            var job = new Samurai();
            var stats = job.BaseBonus;

            Assert.AreEqual(4, stats.Strength, "Samurai base Strength should be 4");
            Assert.AreEqual(2, stats.Agility, "Samurai base Agility should be 2");
            Assert.AreEqual(3, stats.Vitality, "Samurai base Vitality should be 3");
            Assert.AreEqual(1, stats.Magic, "Samurai base Magic should be 1");
        }

        [TestMethod]
        public void Samurai_Level99_ReachesTargetStats()
        {
            var job = new Samurai();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 80, "Samurai Strength at L99");
            AssertWithinTolerance(stats.Agility, 58, "Samurai Agility at L99");
            AssertWithinTolerance(stats.Vitality, 75, "Samurai Vitality at L99");
            AssertWithinTolerance(stats.Magic, 38, "Samurai Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region ShadowFist Tests

        [TestMethod]
        public void ShadowFist_Level1_HasCorrectBaseStats()
        {
            var job = new ShadowFist();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength, "ShadowFist base Strength should be 3");
            Assert.AreEqual(3, stats.Agility, "ShadowFist base Agility should be 3");
            Assert.AreEqual(2, stats.Vitality, "ShadowFist base Vitality should be 2");
            Assert.AreEqual(1, stats.Magic, "ShadowFist base Magic should be 1");
        }

        [TestMethod]
        public void ShadowFist_Level99_ReachesTargetStats()
        {
            var job = new ShadowFist();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 72, "ShadowFist Strength at L99");
            AssertWithinTolerance(stats.Agility, 88, "ShadowFist Agility at L99");
            AssertWithinTolerance(stats.Vitality, 58, "ShadowFist Vitality at L99");
            AssertWithinTolerance(stats.Magic, 65, "ShadowFist Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Shadowmender Tests

        [TestMethod]
        public void Shadowmender_Level1_HasCorrectBaseStats()
        {
            var job = new Shadowmender();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "Shadowmender base Strength should be 2");
            Assert.AreEqual(3, stats.Agility, "Shadowmender base Agility should be 3");
            Assert.AreEqual(2, stats.Vitality, "Shadowmender base Vitality should be 2");
            Assert.AreEqual(2, stats.Magic, "Shadowmender base Magic should be 2");
        }

        [TestMethod]
        public void Shadowmender_Level99_ReachesTargetStats()
        {
            var job = new Shadowmender();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 62, "Shadowmender Strength at L99");
            AssertWithinTolerance(stats.Agility, 85, "Shadowmender Agility at L99");
            AssertWithinTolerance(stats.Vitality, 55, "Shadowmender Vitality at L99");
            AssertWithinTolerance(stats.Magic, 80, "Shadowmender Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Spellcloak Tests

        [TestMethod]
        public void Spellcloak_Level1_HasCorrectBaseStats()
        {
            var job = new Spellcloak();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "Spellcloak base Strength should be 2");
            Assert.AreEqual(2, stats.Agility, "Spellcloak base Agility should be 2");
            Assert.AreEqual(2, stats.Vitality, "Spellcloak base Vitality should be 2");
            Assert.AreEqual(3, stats.Magic, "Spellcloak base Magic should be 3");
        }

        [TestMethod]
        public void Spellcloak_Level99_ReachesTargetStats()
        {
            var job = new Spellcloak();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 62, "Spellcloak Strength at L99");
            AssertWithinTolerance(stats.Agility, 85, "Spellcloak Agility at L99");
            AssertWithinTolerance(stats.Vitality, 52, "Spellcloak Vitality at L99");
            AssertWithinTolerance(stats.Magic, 88, "Spellcloak Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Stalker Tests

        [TestMethod]
        public void Stalker_Level1_HasCorrectBaseStats()
        {
            var job = new Stalker();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "Stalker base Strength should be 2");
            Assert.AreEqual(3, stats.Agility, "Stalker base Agility should be 3");
            Assert.AreEqual(2, stats.Vitality, "Stalker base Vitality should be 2");
            Assert.AreEqual(1, stats.Magic, "Stalker base Magic should be 1");
        }

        [TestMethod]
        public void Stalker_Level99_ReachesTargetStats()
        {
            var job = new Stalker();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 68, "Stalker Strength at L99");
            AssertWithinTolerance(stats.Agility, 90, "Stalker Agility at L99");
            AssertWithinTolerance(stats.Vitality, 58, "Stalker Vitality at L99");
            AssertWithinTolerance(stats.Magic, 65, "Stalker Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region WarMage Tests

        [TestMethod]
        public void WarMage_Level1_HasCorrectBaseStats()
        {
            var job = new WarMage();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength, "WarMage base Strength should be 3");
            Assert.AreEqual(1, stats.Agility, "WarMage base Agility should be 1");
            Assert.AreEqual(2, stats.Vitality, "WarMage base Vitality should be 2");
            Assert.AreEqual(3, stats.Magic, "WarMage base Magic should be 3");
        }

        [TestMethod]
        public void WarMage_Level99_ReachesTargetStats()
        {
            var job = new WarMage();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 60, "WarMage Strength at L99");
            AssertWithinTolerance(stats.Agility, 50, "WarMage Agility at L99");
            AssertWithinTolerance(stats.Vitality, 70, "WarMage Vitality at L99");
            AssertWithinTolerance(stats.Magic, 80, "WarMage Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Wizard Tests

        [TestMethod]
        public void Wizard_Level1_HasCorrectBaseStats()
        {
            var job = new Wizard();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength, "Wizard base Strength should be 2");
            Assert.AreEqual(2, stats.Agility, "Wizard base Agility should be 2");
            Assert.AreEqual(2, stats.Vitality, "Wizard base Vitality should be 2");
            Assert.AreEqual(4, stats.Magic, "Wizard base Magic should be 4");
        }

        [TestMethod]
        public void Wizard_Level99_ReachesTargetStats()
        {
            var job = new Wizard();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 55, "Wizard Strength at L99");
            AssertWithinTolerance(stats.Agility, 75, "Wizard Agility at L99");
            AssertWithinTolerance(stats.Vitality, 50, "Wizard Vitality at L99");
            AssertWithinTolerance(stats.Magic, 98, "Wizard Magic at L99");

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
