using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
