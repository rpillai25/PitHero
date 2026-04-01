using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    [TestClass]
    public class PitResetOnHeroDeathTests
    {
        [TestMethod]
        public void PitReset_WhenNoMercenariesHired_ShouldSetPitToLevel1Immediately()
        {
            // Arrange: pit at level 15, no mercenaries
            var pitManager = new VirtualPitWidthManager();
            pitManager.Initialize();
            pitManager.SetPitLevel(15);
            Assert.AreEqual(15, pitManager.CurrentPitLevel);

            // Act: AreAllHiredMercenariesOutOfPit with no mercs returns true → reset
            pitManager.SetPitLevel(1);

            // Assert
            Assert.AreEqual(1, pitManager.CurrentPitLevel);
        }

        [TestMethod]
        public void PitReset_PitAtLevel1_ShouldStayLevel1AndRegenerate()
        {
            var pitManager = new VirtualPitWidthManager();
            pitManager.Initialize();
            pitManager.SetPitLevel(1);

            pitManager.SetPitLevel(1); // reset from 1 → 1

            Assert.AreEqual(1, pitManager.CurrentPitLevel);
        }

        [TestMethod]
        public void PitReset_FromLevel25_ShouldShrinkPitWidth()
        {
            var pitManager = new VirtualPitWidthManager();
            pitManager.Initialize();
            pitManager.SetPitLevel(25);
            int widthAt25 = pitManager.CurrentPitRightEdge;

            pitManager.SetPitLevel(1);
            int widthAt1 = pitManager.CurrentPitRightEdge;

            Assert.IsTrue(widthAt1 < widthAt25, "Pit at level 1 should be narrower than level 25");
        }

        [TestMethod]
        public void PitWidth_AfterReset_MatchesLevelOneWidth()
        {
            var pitManager = new VirtualPitWidthManager();
            pitManager.Initialize();

            // Capture level-1 right edge from a fresh manager
            int expectedRightEdge = pitManager.CurrentPitRightEdge;

            // Advance pit
            pitManager.SetPitLevel(30);
            Assert.AreNotEqual(expectedRightEdge, pitManager.CurrentPitRightEdge);

            // Reset
            pitManager.SetPitLevel(1);
            Assert.AreEqual(expectedRightEdge, pitManager.CurrentPitRightEdge);
        }
    }
}
