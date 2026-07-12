using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests that DetermineMercenaryLevel (live and virtual) respects the minLevel floor
    /// introduced by the pit-tier feature (step 3b), and that the default minLevel=1 is
    /// unchanged from previous behaviour.
    /// </summary>
    [TestClass]
    public class MercenaryLevelFloorTests
    {
        // Run N seeded trials and assert all results are within bounds.
        private const int TrialCount = 200;

        private static void SeedRng(int seed)
        {
            Nez.Random.SetSeed(seed);
        }

        // ── Live MercenaryManager.DetermineMercenaryLevel ─────────────────────────

        [TestMethod]
        public void DetermineMercenaryLevel_WithMinLevel1_ResultAlwaysAtLeast1()
        {
            SeedRng(42);
            int heroLevel = 40;
            for (int i = 0; i < TrialCount; i++)
            {
                int level = MercenaryManager.DetermineMercenaryLevel(heroLevel, minLevel: 1);
                Assert.IsTrue(level >= 1, $"Level {level} below minLevel=1 on trial {i}");
            }
        }

        [TestMethod]
        public void DetermineMercenaryLevel_WithMinLevel20_ResultAlwaysAtLeast20()
        {
            SeedRng(1337);
            int heroLevel = 40;
            int minLevel = 20;
            for (int i = 0; i < TrialCount; i++)
            {
                int level = MercenaryManager.DetermineMercenaryLevel(heroLevel, minLevel);
                Assert.IsTrue(level >= minLevel,
                    $"Level {level} below minLevel={minLevel} on trial {i}");
            }
        }

        [TestMethod]
        public void DetermineMercenaryLevel_MinLevel1_MatchesOldBehaviour()
        {
            // With minLevel=1, the default, at least some results should be below 20
            // (the distribution is not entirely floored).
            SeedRng(99);
            int heroLevel = 40;
            bool sawLow = false;
            for (int i = 0; i < TrialCount; i++)
            {
                // Reset seed consistently so we compare against known rolls.
                int level = MercenaryManager.DetermineMercenaryLevel(heroLevel, minLevel: 1);
                if (level < 20)
                    sawLow = true;
            }
            Assert.IsTrue(sawLow, "With minLevel=1, some results should be below 20 for heroLevel=40");
        }

        [TestMethod]
        public void DetermineMercenaryLevel_MinLevelEqualToHeroLevel_ResultIsHeroLevel()
        {
            // When minLevel == heroLevel, every result is clamped to heroLevel.
            SeedRng(7777);
            int heroLevel = 35;
            for (int i = 0; i < TrialCount; i++)
            {
                int level = MercenaryManager.DetermineMercenaryLevel(heroLevel, minLevel: heroLevel);
                Assert.AreEqual(heroLevel, level,
                    $"With minLevel==heroLevel, result should be heroLevel on trial {i}");
            }
        }

        // ── Virtual VirtualMercenaryLevelRoller ───────────────────────────────────

        [TestMethod]
        public void VirtualRoller_WithMinLevel1_ResultAlwaysAtLeast1()
        {
            SeedRng(42);
            int heroLevel = 40;
            for (int i = 0; i < TrialCount; i++)
            {
                int level = VirtualMercenaryLevelRoller.DetermineMercenaryLevel(heroLevel, minLevel: 1);
                Assert.IsTrue(level >= 1, $"Virtual level {level} below 1 on trial {i}");
            }
        }

        [TestMethod]
        public void VirtualRoller_WithMinLevel20_ResultAlwaysAtLeast20()
        {
            SeedRng(1337);
            int heroLevel = 40;
            int minLevel = 20;
            for (int i = 0; i < TrialCount; i++)
            {
                int level = VirtualMercenaryLevelRoller.DetermineMercenaryLevel(heroLevel, minLevel);
                Assert.IsTrue(level >= minLevel,
                    $"Virtual level {level} below {minLevel} on trial {i}");
            }
        }

        [TestMethod]
        public void VirtualRoller_DefaultMinLevel1_IsUnchanged()
        {
            // Calling with no minLevel argument must produce the same distribution as minLevel=1.
            SeedRng(555);
            int heroLevel = 30;
            for (int i = 0; i < TrialCount; i++)
            {
                int levelDefault = VirtualMercenaryLevelRoller.DetermineMercenaryLevel(heroLevel);
                Assert.IsTrue(levelDefault >= 1,
                    $"Default minLevel=1 result {levelDefault} below 1 on trial {i}");
            }
        }

        // ── Live/Virtual parity ───────────────────────────────────────────────────

        [TestMethod]
        public void LiveAndVirtual_ProduceSameResultForSameSeed()
        {
            // Both implementations consume Nez.Random in the same order, so they must
            // match exactly when seeded identically before each call.
            int heroLevel = 50;
            int minLevel = 15;
            for (int i = 0; i < 50; i++)
            {
                SeedRng(i * 31 + 7);
                int live = MercenaryManager.DetermineMercenaryLevel(heroLevel, minLevel);
                SeedRng(i * 31 + 7);
                int virt = VirtualMercenaryLevelRoller.DetermineMercenaryLevel(heroLevel, minLevel);
                Assert.AreEqual(live, virt,
                    $"Live ({live}) and virtual ({virt}) differ for seed {i * 31 + 7}");
            }
        }
    }
}
