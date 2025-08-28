using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Test to verify pit shrinkage behavior using the Virtual Game Layer
    /// </summary>
    [TestClass]
    public class PitShrinkageIntegrationTest
    {
        [TestMethod]
        public void VirtualPitWidthManager_ShrinkageScenario_Level20To10_ShouldWorkCorrectly()
        {
            // Arrange - Set up virtual game context
            var virtualWorld = new VirtualWorldState();
            var virtualTiledMapService = new VirtualTiledMapService(virtualWorld, GameConfig.TileSize, GameConfig.TileSize);
            var virtualPitWidthManager = new VirtualPitWidthManager(virtualTiledMapService);
            
            // Initialize the virtual manager
            virtualPitWidthManager.Initialize();
            
            // Verify initial state (level 1)
            Assert.AreEqual(1, virtualPitWidthManager.CurrentPitLevel, "Should start at level 1");
            int initialRightEdge = virtualPitWidthManager.CurrentPitRightEdge;
            int expectedInitialRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth; // 1 + 12 = 13
            Assert.AreEqual(expectedInitialRightEdge, initialRightEdge, "Initial right edge should be base pit size");
            
            // Act 1 - Expand to level 20 (should extend by 4 inner floor tiles + 2 for wall/floor)
            virtualPitWidthManager.SetPitLevel(20);
            int expandedRightEdge = virtualPitWidthManager.CurrentPitRightEdge;
            int expectedExpandedRightEdge = expectedInitialRightEdge + 4 + 2; // 13 + 4 + 2 = 19
            
            // Assert expansion worked
            Assert.AreEqual(20, virtualPitWidthManager.CurrentPitLevel, "Level should be set to 20");
            Assert.AreEqual(expectedExpandedRightEdge, expandedRightEdge, "Level 20 should expand to right edge 19");
            Assert.IsTrue(expandedRightEdge > initialRightEdge, "Pit should be larger at level 20");
            
            // Act 2 - Shrink to level 10 (should extend by 2 inner floor tiles + 2 for wall/floor)
            virtualPitWidthManager.SetPitLevel(10);
            int shrunkRightEdge = virtualPitWidthManager.CurrentPitRightEdge;
            int expectedShrunkRightEdge = expectedInitialRightEdge + 2 + 2; // 13 + 2 + 2 = 17
            
            // Assert shrinkage worked
            Assert.AreEqual(10, virtualPitWidthManager.CurrentPitLevel, "Level should be set to 10");
            Assert.AreEqual(expectedShrunkRightEdge, shrunkRightEdge, "Level 10 should shrink to right edge 17");
            Assert.IsTrue(shrunkRightEdge < expandedRightEdge, "Pit should be smaller at level 10 than level 20");
            Assert.IsTrue(shrunkRightEdge > initialRightEdge, "Pit should still be larger than level 1");
            
            // Act 3 - Shrink further to level 1 (should return to base pit size)
            virtualPitWidthManager.SetPitLevel(1);
            int finalRightEdge = virtualPitWidthManager.CurrentPitRightEdge;
            
            // Assert complete shrinkage to base size
            Assert.AreEqual(1, virtualPitWidthManager.CurrentPitLevel, "Level should be set to 1");
            Assert.AreEqual(expectedInitialRightEdge, finalRightEdge, "Level 1 should return to base pit size");
            Assert.IsTrue(finalRightEdge < shrunkRightEdge, "Pit should be smaller at level 1 than level 10");
            Assert.AreEqual(initialRightEdge, finalRightEdge, "Should return to exact same size as initial");
        }

        [TestMethod] 
        public void VirtualPitWidthManager_MultipleTransitions_ShouldMaintainConsistency()
        {
            // Arrange
            var virtualWorld = new VirtualWorldState();
            var virtualTiledMapService = new VirtualTiledMapService(virtualWorld, GameConfig.TileSize, GameConfig.TileSize);
            var virtualPitWidthManager = new VirtualPitWidthManager(virtualTiledMapService);
            virtualPitWidthManager.Initialize();
            
            // Test sequence: 1 -> 30 -> 20 -> 10 -> 1 -> 25
            var transitions = new int[] { 30, 20, 10, 1, 25 };
            var expectedRightEdges = new int[transitions.Length];
            
            int baseRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth;
            
            // Calculate expected right edges for each level
            for (int i = 0; i < transitions.Length; i++)
            {
                int level = transitions[i];
                int innerFloors = ((int)(level / 10)) * 2;
                expectedRightEdges[i] = innerFloors > 0 ? baseRightEdge + innerFloors + 2 : baseRightEdge;
            }
            
            // Act & Assert each transition
            for (int i = 0; i < transitions.Length; i++)
            {
                int level = transitions[i];
                int expectedRightEdge = expectedRightEdges[i];
                
                virtualPitWidthManager.SetPitLevel(level);
                int actualRightEdge = virtualPitWidthManager.CurrentPitRightEdge;
                
                Assert.AreEqual(level, virtualPitWidthManager.CurrentPitLevel, 
                    $"Transition {i}: Level should be {level}");
                Assert.AreEqual(expectedRightEdge, actualRightEdge, 
                    $"Transition {i}: Level {level} should have right edge {expectedRightEdge}");
            }
        }
    }
}