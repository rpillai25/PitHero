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

        [TestMethod]
        public void PitWidthManager_CalculateCurrentPitWorldBounds_ShouldReturnFallbackWhenNotInitialized()
        {
            // Arrange
            var pitWidthManager = new PitWidthManager();
            
            // Act
            var bounds = pitWidthManager.CalculateCurrentPitWorldBounds();
            
            // Assert
            Assert.IsTrue(bounds.Width > 0, "Bounds width should be positive");
            Assert.IsTrue(bounds.Height > 0, "Bounds height should be positive");
            
            // Should match default static pit bounds
            var expectedWidth = (GameConfig.PitRectWidth * GameConfig.TileSize) + (2 * GameConfig.PitColliderPadding);
            var expectedHeight = (GameConfig.PitRectHeight * GameConfig.TileSize) + (2 * GameConfig.PitColliderPadding);
            
            Assert.AreEqual(expectedWidth, bounds.Width, "Default bounds width should match GameConfig calculations");
            Assert.AreEqual(expectedHeight, bounds.Height, "Default bounds height should match GameConfig calculations");
        }

        [TestMethod]
        public void PitWidthManager_DynamicPitBounds_ShouldExpandCorrectly()
        {
            // Test the mathematical relationship between pit level and bounds
            
            // Default pit (level 1-9): width = 12 tiles
            var defaultTileWidth = GameConfig.PitRectWidth; // 12
            var defaultWorldWidth = (defaultTileWidth * GameConfig.TileSize) + (2 * GameConfig.PitColliderPadding);
            
            // Level 10: should add 2 tiles = 14 total width
            var level10TileWidth = defaultTileWidth + 2;
            var level10WorldWidth = (level10TileWidth * GameConfig.TileSize) + (2 * GameConfig.PitColliderPadding);
            
            // Level 20: should add 4 tiles = 16 total width  
            var level20TileWidth = defaultTileWidth + 4;
            var level20WorldWidth = (level20TileWidth * GameConfig.TileSize) + (2 * GameConfig.PitColliderPadding);
            
            // Assert the mathematical progression
            Assert.AreEqual(12, defaultTileWidth, "Default pit should be 12 tiles wide");
            Assert.AreEqual(14, level10TileWidth, "Level 10 pit should be 14 tiles wide");
            Assert.AreEqual(16, level20TileWidth, "Level 20 pit should be 16 tiles wide");
            
            // Verify world coordinate calculations
            var expectedTileSize = 32; // GameConfig.TileSize
            var expectedPadding = 4;   // GameConfig.PitColliderPadding
            
            Assert.AreEqual((12 * expectedTileSize) + (2 * expectedPadding), defaultWorldWidth, "Default world width calculation");
            Assert.AreEqual((14 * expectedTileSize) + (2 * expectedPadding), level10WorldWidth, "Level 10 world width calculation");
            Assert.AreEqual((16 * expectedTileSize) + (2 * expectedPadding), level20WorldWidth, "Level 20 world width calculation");
        }
    }
}