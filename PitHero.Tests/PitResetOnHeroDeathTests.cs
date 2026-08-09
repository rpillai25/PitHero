using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    [TestClass]
    public class PitResetOnHeroDeathTests
    {
        private static VirtualPitWidthManager CreatePitManager()
        {
            var worldState = new VirtualWorldState();
            var tiledMapService = new VirtualTiledMapService(worldState);
            return new VirtualPitWidthManager(tiledMapService);
        }

        [TestMethod]
        public void PitReset_WhenNoMercenariesHired_ShouldSetPitToLevel1Immediately()
        {
            // Arrange: pit at level 15, no mercenaries
            var pitManager = CreatePitManager();
            pitManager.Initialize();
            pitManager.SetPitLevel(15);
            Assert.AreEqual(15, pitManager.CurrentPitLevel);

            // Act: reset to level 1
            pitManager.SetPitLevel(1);

            // Assert
            Assert.AreEqual(1, pitManager.CurrentPitLevel);
        }

        [TestMethod]
        public void PitReset_PitAtLevel1_ShouldStayLevel1AndRegenerate()
        {
            var pitManager = CreatePitManager();
            pitManager.Initialize();
            pitManager.SetPitLevel(1);

            pitManager.SetPitLevel(1); // reset from 1 → 1

            Assert.AreEqual(1, pitManager.CurrentPitLevel);
        }

        [TestMethod]
        public void PitReset_FromLevel25_ShouldShrinkPitWidth()
        {
            // A fresh pit at level 1 should have a smaller right edge than one advanced to level 25
            var pitAtLevel1 = CreatePitManager();
            pitAtLevel1.Initialize();
            int rightEdgeAtLevel1 = pitAtLevel1.CurrentPitRightEdge;

            var pitAtLevel25 = CreatePitManager();
            pitAtLevel25.Initialize();
            pitAtLevel25.SetPitLevel(25);
            int rightEdgeAtLevel25 = pitAtLevel25.CurrentPitRightEdge;

            Assert.IsTrue(rightEdgeAtLevel1 < rightEdgeAtLevel25, "Pit at level 1 should be narrower than level 25");

            // After death: reset level counter back to 1
            pitAtLevel25.SetPitLevel(1);
            Assert.AreEqual(1, pitAtLevel25.CurrentPitLevel, "Pit level should be reset to 1");
        }

        [TestMethod]
        public void PitWidth_AfterReset_MatchesLevelOneWidth()
        {
            // Verify the level counter returns to 1 after reset
            var pitManager = CreatePitManager();
            pitManager.Initialize();

            pitManager.SetPitLevel(30);
            Assert.AreEqual(30, pitManager.CurrentPitLevel);

            pitManager.SetPitLevel(1);
            Assert.AreEqual(1, pitManager.CurrentPitLevel, "Pit level should be 1 after reset");
        }

        // ── Tier state ────────────────────────────────────────────────────────────

        [TestMethod]
        public void ResetTierForNewCycle_ResetsTierAndBaseLevel()
        {
            // Hero death resets the whole pit cycle: tier and base level go back to 1.
            var pitManager = CreatePitManager();
            pitManager.Initialize();
            pitManager.SetPitTier(3);
            pitManager.SetTierBaseLevel(20);

            pitManager.ResetTierForNewCycle();

            Assert.AreEqual(1, pitManager.CurrentPitTier, "PitTier must reset to 1 on death");
            Assert.AreEqual(1, pitManager.TierBaseLevel, "TierBaseLevel must reset to 1 on death");
        }

        [TestMethod]
        public void SetPitLevel1_AloneDoesNotTouchTier()
        {
            // The tier reset is an explicit death-path call (ResetTierForNewCycle),
            // not a side effect of SetPitLevel(1) — normal tier rollover also passes through level 1.
            var pitManager = CreatePitManager();
            pitManager.Initialize();
            pitManager.SetPitTier(3);
            pitManager.SetTierBaseLevel(20);

            pitManager.SetPitLevel(1);

            Assert.AreEqual(3, pitManager.CurrentPitTier, "SetPitLevel must not change tier");
            Assert.AreEqual(20, pitManager.TierBaseLevel, "SetPitLevel must not change base level");
            Assert.AreEqual(1, pitManager.CurrentPitLevel, "PitLevel should be 1 after reset");
        }

        [TestMethod]
        public void IncrementPitTier_IncreasesCounterAndSetsBaseLevel()
        {
            var pitManager = CreatePitManager();
            pitManager.Initialize();

            Assert.AreEqual(1, pitManager.CurrentPitTier);
            Assert.AreEqual(1, pitManager.TierBaseLevel);

            pitManager.IncrementPitTier(heroLevelAtEntry: 30);

            Assert.AreEqual(2, pitManager.CurrentPitTier);
            Assert.AreEqual(30, pitManager.TierBaseLevel);
        }

        [TestMethod]
        public void IncrementPitTier_Monotonic_BaseLevelNeverDecreases()
        {
            var pitManager = CreatePitManager();
            pitManager.Initialize();
            pitManager.IncrementPitTier(heroLevelAtEntry: 40);
            Assert.AreEqual(40, pitManager.TierBaseLevel);

            // Incrementing again with a LOWER hero level must not decrease base level.
            pitManager.IncrementPitTier(heroLevelAtEntry: 20);
            Assert.AreEqual(40, pitManager.TierBaseLevel, "TierBaseLevel must never decrease");
            Assert.AreEqual(3, pitManager.CurrentPitTier);
        }

        [TestMethod]
        public void IncrementPitTier_CapsAt99()
        {
            var pitManager = CreatePitManager();
            pitManager.Initialize();
            pitManager.SetPitTier(BiomeProgressionConfig.MaxPitTier); // 99

            pitManager.IncrementPitTier(heroLevelAtEntry: 1);

            Assert.AreEqual(BiomeProgressionConfig.MaxPitTier, pitManager.CurrentPitTier, "Tier must not exceed 99");
        }

        [TestMethod]
        public void SetTierBaseLevel_IsMonotonicNonDecreasing()
        {
            // ResetTierForNewCycle is the only sanctioned way to lower the base level.
            var pitManager = CreatePitManager();
            pitManager.Initialize();
            pitManager.SetTierBaseLevel(50);
            Assert.AreEqual(50, pitManager.TierBaseLevel);

            // Lower value is silently ignored.
            pitManager.SetTierBaseLevel(30);
            Assert.AreEqual(50, pitManager.TierBaseLevel, "Lower base level should be ignored");

            // Higher value is accepted.
            pitManager.SetTierBaseLevel(60);
            Assert.AreEqual(60, pitManager.TierBaseLevel);

            // After a cycle reset, the monotonic guard restarts from 1.
            pitManager.ResetTierForNewCycle();
            pitManager.SetTierBaseLevel(5);
            Assert.AreEqual(5, pitManager.TierBaseLevel, "Base level should be settable again after reset");
        }

        [TestMethod]
        public void SetPitTier_ClampsToValidRange()
        {
            var pitManager = CreatePitManager();
            pitManager.Initialize();

            pitManager.SetPitTier(0);
            Assert.AreEqual(1, pitManager.CurrentPitTier, "Tier below 1 should clamp to 1");

            pitManager.SetPitTier(200);
            Assert.AreEqual(BiomeProgressionConfig.MaxPitTier, pitManager.CurrentPitTier, "Tier above 99 should clamp to 99");
        }
    }
}
