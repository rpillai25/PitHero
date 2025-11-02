using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Tertiary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for tertiary job stat growth curves to ensure they reach target values at various levels.
    /// Tests validate that stats stay within caps and reach targets with ±5% tolerance at level 99.
    /// Tertiary jobs should be 25-40% stronger than primaries and 10-15% stronger than secondaries.
    /// </summary>
    [TestClass]
    public class TertiaryJobStatGrowthTests
    {
        private const double TolerancePercent = 0.05; // 5% tolerance

        #region Helper Methods

        private void AssertWithinTolerance(int actual, int target, string message)
        {
            int tolerance = (int)Math.Ceiling(target * TolerancePercent);
            int minValue = target - tolerance;
            int maxValue = target + tolerance;
            Assert.IsTrue(actual >= minValue && actual <= maxValue,
                $"{message}: Expected {target} (±{tolerance}), got {actual}");
        }

        private void AssertWithinCaps(in StatBlock stats)
        {
            Assert.IsTrue(stats.Strength <= StatConstants.MaxStat, $"Strength {stats.Strength} exceeds cap");
            Assert.IsTrue(stats.Agility <= StatConstants.MaxStat, $"Agility {stats.Agility} exceeds cap");
            Assert.IsTrue(stats.Vitality <= StatConstants.MaxStat, $"Vitality {stats.Vitality} exceeds cap");
            Assert.IsTrue(stats.Magic <= StatConstants.MaxStat, $"Magic {stats.Magic} exceeds cap");
        }

        #endregion

        #region ArcaneSamurai Tests

        [TestMethod]
        public void ArcaneSamurai_Level1_HasCorrectBaseStats()
        {
            var job = new ArcaneSamurai();
            var stats = job.BaseBonus;

            Assert.AreEqual(4, stats.Strength);
            Assert.AreEqual(3, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void ArcaneSamurai_Level99_ReachesTargetStats()
        {
            var job = new ArcaneSamurai();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 82, "ArcaneSamurai Strength at L99");
            AssertWithinTolerance(stats.Agility, 72, "ArcaneSamurai Agility at L99");
            AssertWithinTolerance(stats.Vitality, 78, "ArcaneSamurai Vitality at L99");
            AssertWithinTolerance(stats.Magic, 88, "ArcaneSamurai Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region ArcaneStalker Tests

        [TestMethod]
        public void ArcaneStalker_Level1_HasCorrectBaseStats()
        {
            var job = new ArcaneStalker();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(3, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(4, stats.Magic);
        }

        [TestMethod]
        public void ArcaneStalker_Level99_ReachesTargetStats()
        {
            var job = new ArcaneStalker();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 70, "ArcaneStalker Strength at L99");
            AssertWithinTolerance(stats.Agility, 85, "ArcaneStalker Agility at L99");
            AssertWithinTolerance(stats.Vitality, 72, "ArcaneStalker Vitality at L99");
            AssertWithinTolerance(stats.Magic, 92, "ArcaneStalker Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region DivineArcher Tests

        [TestMethod]
        public void DivineArcher_Level1_HasCorrectBaseStats()
        {
            var job = new DivineArcher();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(2, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(5, stats.Magic);
        }

        [TestMethod]
        public void DivineArcher_Level99_ReachesTargetStats()
        {
            var job = new DivineArcher();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 75, "DivineArcher Strength at L99");
            AssertWithinTolerance(stats.Agility, 82, "DivineArcher Agility at L99");
            AssertWithinTolerance(stats.Vitality, 85, "DivineArcher Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "DivineArcher Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region DivineCloak Tests

        [TestMethod]
        public void DivineCloak_Level1_HasCorrectBaseStats()
        {
            var job = new DivineCloak();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(4, stats.Magic);
        }

        [TestMethod]
        public void DivineCloak_Level99_ReachesTargetStats()
        {
            var job = new DivineCloak();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 72, "DivineCloak Strength at L99");
            AssertWithinTolerance(stats.Agility, 88, "DivineCloak Agility at L99");
            AssertWithinTolerance(stats.Vitality, 72, "DivineCloak Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "DivineCloak Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region DivineSamurai Tests

        [TestMethod]
        public void DivineSamurai_Level1_HasCorrectBaseStats()
        {
            var job = new DivineSamurai();
            var stats = job.BaseBonus;

            Assert.AreEqual(4, stats.Strength);
            Assert.AreEqual(2, stats.Agility);
            Assert.AreEqual(3, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void DivineSamurai_Level99_ReachesTargetStats()
        {
            var job = new DivineSamurai();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 84, "DivineSamurai Strength at L99");
            AssertWithinTolerance(stats.Agility, 70, "DivineSamurai Agility at L99");
            AssertWithinTolerance(stats.Vitality, 88, "DivineSamurai Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "DivineSamurai Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region DragonMarksman Tests

        [TestMethod]
        public void DragonMarksman_Level1_HasCorrectBaseStats()
        {
            var job = new DragonMarksman();
            var stats = job.BaseBonus;

            Assert.AreEqual(4, stats.Strength);
            Assert.AreEqual(2, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void DragonMarksman_Level99_ReachesTargetStats()
        {
            var job = new DragonMarksman();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 85, "DragonMarksman Strength at L99");
            AssertWithinTolerance(stats.Agility, 85, "DragonMarksman Agility at L99");
            AssertWithinTolerance(stats.Vitality, 80, "DragonMarksman Vitality at L99");
            AssertWithinTolerance(stats.Magic, 75, "DragonMarksman Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region HolyShadow Tests

        [TestMethod]
        public void HolyShadow_Level1_HasCorrectBaseStats()
        {
            var job = new HolyShadow();
            var stats = job.BaseBonus;

            Assert.AreEqual(1, stats.Strength);
            Assert.AreEqual(3, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(4, stats.Magic);
        }

        [TestMethod]
        public void HolyShadow_Level99_ReachesTargetStats()
        {
            var job = new HolyShadow();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 72, "HolyShadow Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "HolyShadow Agility at L99");
            AssertWithinTolerance(stats.Vitality, 78, "HolyShadow Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "HolyShadow Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region KiNinja Tests

        [TestMethod]
        public void KiNinja_Level1_HasCorrectBaseStats()
        {
            var job = new KiNinja();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(2, stats.Magic);
        }

        [TestMethod]
        public void KiNinja_Level99_ReachesTargetStats()
        {
            var job = new KiNinja();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 82, "KiNinja Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "KiNinja Agility at L99");
            AssertWithinTolerance(stats.Vitality, 75, "KiNinja Vitality at L99");
            AssertWithinTolerance(stats.Magic, 80, "KiNinja Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region MarksmanWizard Tests

        [TestMethod]
        public void MarksmanWizard_Level1_HasCorrectBaseStats()
        {
            var job = new MarksmanWizard();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(2, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(5, stats.Magic);
        }

        [TestMethod]
        public void MarksmanWizard_Level99_ReachesTargetStats()
        {
            var job = new MarksmanWizard();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 80, "MarksmanWizard Strength at L99");
            AssertWithinTolerance(stats.Agility, 90, "MarksmanWizard Agility at L99");
            AssertWithinTolerance(stats.Vitality, 85, "MarksmanWizard Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "MarksmanWizard Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region MysticAvenger Tests

        [TestMethod]
        public void MysticAvenger_Level1_HasCorrectBaseStats()
        {
            var job = new MysticAvenger();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(4, stats.Magic);
        }

        [TestMethod]
        public void MysticAvenger_Level99_ReachesTargetStats()
        {
            var job = new MysticAvenger();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 80, "MysticAvenger Strength at L99");
            AssertWithinTolerance(stats.Agility, 90, "MysticAvenger Agility at L99");
            AssertWithinTolerance(stats.Vitality, 72, "MysticAvenger Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "MysticAvenger Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region MysticMarksman Tests

        [TestMethod]
        public void MysticMarksman_Level1_HasCorrectBaseStats()
        {
            var job = new MysticMarksman();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(3, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(4, stats.Magic);
        }

        [TestMethod]
        public void MysticMarksman_Level99_ReachesTargetStats()
        {
            var job = new MysticMarksman();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 78, "MysticMarksman Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "MysticMarksman Agility at L99");
            AssertWithinTolerance(stats.Vitality, 72, "MysticMarksman Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "MysticMarksman Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region MysticStalker Tests

        [TestMethod]
        public void MysticStalker_Level1_HasCorrectBaseStats()
        {
            var job = new MysticStalker();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void MysticStalker_Level99_ReachesTargetStats()
        {
            var job = new MysticStalker();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 72, "MysticStalker Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "MysticStalker Agility at L99");
            AssertWithinTolerance(stats.Vitality, 72, "MysticStalker Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "MysticStalker Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region SeraphHunter Tests

        [TestMethod]
        public void SeraphHunter_Level1_HasCorrectBaseStats()
        {
            var job = new SeraphHunter();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(3, stats.Agility);
            Assert.AreEqual(3, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void SeraphHunter_Level99_ReachesTargetStats()
        {
            var job = new SeraphHunter();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 82, "SeraphHunter Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "SeraphHunter Agility at L99");
            AssertWithinTolerance(stats.Vitality, 85, "SeraphHunter Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "SeraphHunter Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region ShadowAvenger Tests

        [TestMethod]
        public void ShadowAvenger_Level1_HasCorrectBaseStats()
        {
            var job = new ShadowAvenger();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void ShadowAvenger_Level99_ReachesTargetStats()
        {
            var job = new ShadowAvenger();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 75, "ShadowAvenger Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "ShadowAvenger Agility at L99");
            AssertWithinTolerance(stats.Vitality, 80, "ShadowAvenger Vitality at L99");
            AssertWithinTolerance(stats.Magic, 92, "ShadowAvenger Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region ShadowPaladin Tests

        [TestMethod]
        public void ShadowPaladin_Level1_HasCorrectBaseStats()
        {
            var job = new ShadowPaladin();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(3, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void ShadowPaladin_Level99_ReachesTargetStats()
        {
            var job = new ShadowPaladin();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 82, "ShadowPaladin Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "ShadowPaladin Agility at L99");
            AssertWithinTolerance(stats.Vitality, 85, "ShadowPaladin Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "ShadowPaladin Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region ShinobiMaster Tests

        [TestMethod]
        public void ShinobiMaster_Level1_HasCorrectBaseStats()
        {
            var job = new ShinobiMaster();
            var stats = job.BaseBonus;

            Assert.AreEqual(4, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(3, stats.Vitality);
            Assert.AreEqual(2, stats.Magic);
        }

        [TestMethod]
        public void ShinobiMaster_Level99_ReachesTargetStats()
        {
            var job = new ShinobiMaster();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 88, "ShinobiMaster Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "ShinobiMaster Agility at L99");
            AssertWithinTolerance(stats.Vitality, 88, "ShinobiMaster Vitality at L99");
            AssertWithinTolerance(stats.Magic, 80, "ShinobiMaster Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region SilentHunter Tests

        [TestMethod]
        public void SilentHunter_Level1_HasCorrectBaseStats()
        {
            var job = new SilentHunter();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(1, stats.Magic);
        }

        [TestMethod]
        public void SilentHunter_Level99_ReachesTargetStats()
        {
            var job = new SilentHunter();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 80, "SilentHunter Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "SilentHunter Agility at L99");
            AssertWithinTolerance(stats.Vitality, 78, "SilentHunter Vitality at L99");
            AssertWithinTolerance(stats.Magic, 80, "SilentHunter Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region SoulGuardian Tests

        [TestMethod]
        public void SoulGuardian_Level1_HasCorrectBaseStats()
        {
            var job = new SoulGuardian();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(4, stats.Magic);
        }

        [TestMethod]
        public void SoulGuardian_Level99_ReachesTargetStats()
        {
            var job = new SoulGuardian();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 72, "SoulGuardian Strength at L99");
            AssertWithinTolerance(stats.Agility, 88, "SoulGuardian Agility at L99");
            AssertWithinTolerance(stats.Vitality, 72, "SoulGuardian Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "SoulGuardian Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region SpellSniper Tests

        [TestMethod]
        public void SpellSniper_Level1_HasCorrectBaseStats()
        {
            var job = new SpellSniper();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(2, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(6, stats.Magic);
        }

        [TestMethod]
        public void SpellSniper_Level99_ReachesTargetStats()
        {
            var job = new SpellSniper();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 82, "SpellSniper Strength at L99");
            AssertWithinTolerance(stats.Agility, 90, "SpellSniper Agility at L99");
            AssertWithinTolerance(stats.Vitality, 85, "SpellSniper Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "SpellSniper Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region StalkerMonk Tests

        [TestMethod]
        public void StalkerMonk_Level1_HasCorrectBaseStats()
        {
            var job = new StalkerMonk();
            var stats = job.BaseBonus;

            Assert.AreEqual(2, stats.Strength);
            Assert.AreEqual(4, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(2, stats.Magic);
        }

        [TestMethod]
        public void StalkerMonk_Level99_ReachesTargetStats()
        {
            var job = new StalkerMonk();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 80, "StalkerMonk Strength at L99");
            AssertWithinTolerance(stats.Agility, 99, "StalkerMonk Agility at L99");
            AssertWithinTolerance(stats.Vitality, 75, "StalkerMonk Vitality at L99");
            AssertWithinTolerance(stats.Magic, 80, "StalkerMonk Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Templar Tests

        [TestMethod]
        public void Templar_Level1_HasCorrectBaseStats()
        {
            var job = new Templar();
            var stats = job.BaseBonus;

            Assert.AreEqual(5, stats.Strength);
            Assert.AreEqual(2, stats.Agility);
            Assert.AreEqual(4, stats.Vitality);
            Assert.AreEqual(3, stats.Magic);
        }

        [TestMethod]
        public void Templar_Level99_ReachesTargetStats()
        {
            var job = new Templar();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 85, "Templar Strength at L99");
            AssertWithinTolerance(stats.Agility, 80, "Templar Agility at L99");
            AssertWithinTolerance(stats.Vitality, 99, "Templar Vitality at L99");
            AssertWithinTolerance(stats.Magic, 99, "Templar Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Trickshot Tests

        [TestMethod]
        public void Trickshot_Level1_HasCorrectBaseStats()
        {
            var job = new Trickshot();
            var stats = job.BaseBonus;

            Assert.AreEqual(3, stats.Strength);
            Assert.AreEqual(3, stats.Agility);
            Assert.AreEqual(2, stats.Vitality);
            Assert.AreEqual(2, stats.Magic);
        }

        [TestMethod]
        public void Trickshot_Level99_ReachesTargetStats()
        {
            var job = new Trickshot();
            var stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

            AssertWithinTolerance(stats.Strength, 80, "Trickshot Strength at L99");
            AssertWithinTolerance(stats.Agility, 92, "Trickshot Agility at L99");
            AssertWithinTolerance(stats.Vitality, 75, "Trickshot Vitality at L99");
            AssertWithinTolerance(stats.Magic, 80, "Trickshot Magic at L99");

            AssertWithinCaps(stats);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void All_Tertiary_Jobs_Respect_Stat_Caps()
        {
            var jobs = new BaseJob[]
            {
                new ArcaneSamurai(), new ArcaneStalker(), new DivineArcher(),
                new DivineCloak(), new DivineSamurai(), new DragonMarksman(),
                new HolyShadow(), new KiNinja(), new MarksmanWizard(),
                new MysticAvenger(), new MysticMarksman(), new MysticStalker(),
                new SeraphHunter(), new ShadowAvenger(), new ShadowPaladin(),
                new ShinobiMaster(), new SilentHunter(), new SoulGuardian(),
                new SpellSniper(), new StalkerMonk(), new Templar(), new Trickshot()
            };

            foreach (var job in jobs)
            {
                for (int level = 1; level <= StatConstants.MaxLevel; level++)
                {
                    var stats = job.GetJobContributionAtLevel(level);
                    Assert.IsTrue(stats.Strength <= StatConstants.MaxStat,
                        $"{job.Name} Strength at L{level} exceeds cap: {stats.Strength}");
                    Assert.IsTrue(stats.Agility <= StatConstants.MaxStat,
                        $"{job.Name} Agility at L{level} exceeds cap: {stats.Agility}");
                    Assert.IsTrue(stats.Vitality <= StatConstants.MaxStat,
                        $"{job.Name} Vitality at L{level} exceeds cap: {stats.Vitality}");
                    Assert.IsTrue(stats.Magic <= StatConstants.MaxStat,
                        $"{job.Name} Magic at L{level} exceeds cap: {stats.Magic}");
                }
            }
        }

        [TestMethod]
        public void All_Tertiary_Jobs_Have_Valid_Growth_Curves()
        {
            var jobs = new BaseJob[]
            {
                new ArcaneSamurai(), new ArcaneStalker(), new DivineArcher(),
                new DivineCloak(), new DivineSamurai(), new DragonMarksman(),
                new HolyShadow(), new KiNinja(), new MarksmanWizard(),
                new MysticAvenger(), new MysticMarksman(), new MysticStalker(),
                new SeraphHunter(), new ShadowAvenger(), new ShadowPaladin(),
                new ShinobiMaster(), new SilentHunter(), new SoulGuardian(),
                new SpellSniper(), new StalkerMonk(), new Templar(), new Trickshot()
            };

            foreach (var job in jobs)
            {
                var level1Stats = job.GetJobContributionAtLevel(1);
                var level99Stats = job.GetJobContributionAtLevel(StatConstants.MaxLevel);

                // Ensure stats increase over levels
                Assert.IsTrue(level99Stats.Strength >= level1Stats.Strength,
                    $"{job.Name} Strength should increase from L1 to L99");
                Assert.IsTrue(level99Stats.Agility >= level1Stats.Agility,
                    $"{job.Name} Agility should increase from L1 to L99");
                Assert.IsTrue(level99Stats.Vitality >= level1Stats.Vitality,
                    $"{job.Name} Vitality should increase from L1 to L99");
                Assert.IsTrue(level99Stats.Magic >= level1Stats.Magic,
                    $"{job.Name} Magic should increase from L1 to L99");
            }
        }

        #endregion
    }
}
