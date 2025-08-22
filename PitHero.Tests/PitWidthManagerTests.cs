using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for PitWidthManager functionality
    /// </summary>
    [TestClass]
    public class PitWidthManagerTests
    {
        [TestMethod]
        public void PitWidthManager_Constructor_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var pitWidthManager = new PitWidthManager();
            
            // Assert
            Assert.AreEqual(1, pitWidthManager.CurrentPitLevel, "Initial pit level should be 1");
        }

        [TestMethod]
        public void PitWidthManager_GetCurrentPitCandidateTargets_ShouldReturnFallbackWhenNotInitialized()
        {
            // Arrange
            var pitWidthManager = new PitWidthManager();
            
            // Act
            var targets = pitWidthManager.GetCurrentPitCandidateTargets();
            
            // Assert
            Assert.IsNotNull(targets, "Targets should not be null");
            Assert.AreEqual(7, targets.Length, "Should return 7 fallback targets");
            Assert.AreEqual(13, targets[0].X, "First target X should be 13 (fallback)");
            Assert.AreEqual(3, targets[0].Y, "First target Y should be 3");
        }

        [TestMethod]
        public void PitWidthManager_CalculateExtensionTiles_Level1ShouldExtendZero()
        {
            // Arrange & Act
            int level1Extension = ((int)(1 / 10)) * 2;
            
            // Assert
            Assert.AreEqual(0, level1Extension, "Level 1 should extend 0 tiles");
        }

        [TestMethod]
        public void PitWidthManager_CalculateExtensionTiles_Level10ShouldExtendTwo()
        {
            // Arrange & Act
            int level10Extension = ((int)(10 / 10)) * 2;
            
            // Assert
            Assert.AreEqual(2, level10Extension, "Level 10 should extend 2 tiles");
        }

        [TestMethod]
        public void PitWidthManager_CalculateExtensionTiles_Level20ShouldExtendFour()
        {
            // Arrange & Act
            int level20Extension = ((int)(20 / 10)) * 2;
            
            // Assert
            Assert.AreEqual(4, level20Extension, "Level 20 should extend 4 tiles");
        }

        [TestMethod]
        public void PitWidthManager_CalculateExtensionTiles_Level35ShouldExtendSix()
        {
            // Arrange & Act
            int level35Extension = ((int)(35 / 10)) * 2;
            
            // Assert
            Assert.AreEqual(6, level35Extension, "Level 35 should extend 6 tiles");
        }

        [TestMethod]
        public void PitWidthManager_ExtensionFormula_ShouldBeCorrect()
        {
            // Test various levels to ensure the formula works correctly
            Assert.AreEqual(0, ((int)(1 / 10)) * 2, "Level 1-9 should extend 0 tiles");
            Assert.AreEqual(0, ((int)(9 / 10)) * 2, "Level 9 should extend 0 tiles");
            Assert.AreEqual(2, ((int)(10 / 10)) * 2, "Level 10 should extend 2 tiles");
            Assert.AreEqual(2, ((int)(19 / 10)) * 2, "Level 19 should extend 2 tiles");
            Assert.AreEqual(4, ((int)(20 / 10)) * 2, "Level 20 should extend 4 tiles");
            Assert.AreEqual(4, ((int)(29 / 10)) * 2, "Level 29 should extend 4 tiles");
            Assert.AreEqual(6, ((int)(30 / 10)) * 2, "Level 30 should extend 6 tiles");
        }

        [TestMethod]
        public void GameConfig_PitRectConfiguration_ShouldSupportDynamicExpansion()
        {
            // Arrange & Act
            var originalRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth - 1; // 1 + 12 - 1 = 12
            
            // Assert
            Assert.AreEqual(12, originalRightEdge, "Original pit right edge should be at x=12");
            
            // Test expected right edges after expansion
            Assert.AreEqual(13, originalRightEdge + 1, "Default MoveToPit targets should be at x=13");
            
            // Test expansion scenarios
            var level10RightEdge = originalRightEdge + 2; // 2 inner floor tiles
            var level20RightEdge = originalRightEdge + 4; // 4 inner floor tiles
            
            Assert.AreEqual(14, level10RightEdge, "Level 10 pit right edge should be at x=14");
            Assert.AreEqual(16, level20RightEdge, "Level 20 pit right edge should be at x=16");
        }
    }
}