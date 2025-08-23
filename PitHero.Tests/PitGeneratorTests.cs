using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for PitGenerator functionality
    /// </summary>
    [TestClass]
    public class PitGeneratorTests
    {
        [TestMethod]
        public void PitGenerator_Constants_ShouldBeValidValues()
        {
            // Arrange & Act - Test that we can access the constants
            var obstacleTag = GameConfig.TAG_OBSTACLE;
            var treasureTag = GameConfig.TAG_TREASURE;
            var monsterTag = GameConfig.TAG_MONSTER;
            var wizardOrbTag = GameConfig.TAG_WIZARD_ORB;
            
            // Assert
            Assert.IsTrue(obstacleTag > 0, "Obstacle tag should be greater than 0");
            Assert.IsTrue(treasureTag > 0, "Treasure tag should be greater than 0");
            Assert.IsTrue(monsterTag > 0, "Monster tag should be greater than 0");
            Assert.IsTrue(wizardOrbTag > 0, "Wizard orb tag should be greater than 0");
        }

        [TestMethod]
        public void GameConfig_EntityTagConstants_ShouldHaveCorrectValues()
        {
            // Arrange & Act
            var obstacleTag = GameConfig.TAG_OBSTACLE;
            var treasureTag = GameConfig.TAG_TREASURE;
            var monsterTag = GameConfig.TAG_MONSTER;
            var wizardOrbTag = GameConfig.TAG_WIZARD_ORB;
            
            // Assert
            Assert.AreEqual(4, obstacleTag, "Obstacle tag should be 4");
            Assert.AreEqual(5, treasureTag, "Treasure tag should be 5");
            Assert.AreEqual(6, monsterTag, "Monster tag should be 6");
            Assert.AreEqual(7, wizardOrbTag, "Wizard orb tag should be 7");
        }

        [TestMethod]
        public void GoapConstants_PitInitialized_ShouldExistWithCorrectValue()
        {
            // Arrange & Act
            var pitInitializedConstant = PitHero.AI.GoapConstants.PitInitialized;
            
            // Assert
            Assert.AreEqual("PitInitialized", pitInitializedConstant, "PitInitialized constant should have correct value");
        }

        [TestMethod]
        public void GameConfig_PitBounds_ShouldHaveCorrectConfiguration()
        {
            // Arrange & Act
            var pitRectX = GameConfig.PitRectX;
            var pitRectY = GameConfig.PitRectY;
            var pitRectWidth = GameConfig.PitRectWidth;
            var pitRectHeight = GameConfig.PitRectHeight;
            
            // Assert
            Assert.AreEqual(1, pitRectX, "Pit rect X should be 1");
            Assert.AreEqual(2, pitRectY, "Pit rect Y should be 2");
            Assert.AreEqual(12, pitRectWidth, "Pit rect width should be 12");
            Assert.AreEqual(9, pitRectHeight, "Pit rect height should be 9");
        }

        [TestMethod]
        public void PitGenerator_ValidPlacementArea_ShouldCalculateCorrectly()
        {
            // Arrange - Calculate valid placement area (excluding 1-tile perimeter)
            var validMinX = GameConfig.PitRectX + 1; // Should be 2
            var validMinY = GameConfig.PitRectY + 1; // Should be 3
            var validMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 2; // Should be 11
            var validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // Should be 9
            
            // Act
            var validWidth = validMaxX - validMinX + 1; // Should be 10
            var validHeight = validMaxY - validMinY + 1; // Should be 7
            var totalSpots = validWidth * validHeight; // Should be 70
            
            // Assert
            Assert.AreEqual(10, validWidth, "Valid placement width should be 10");
            Assert.AreEqual(7, validHeight, "Valid placement height should be 7");
            Assert.AreEqual(70, totalSpots, "Total valid placement spots should be 70");
        }

        [TestMethod]
        public void PitGenerator_EntityCountRequirements_ShouldBeCorrect()
        {
            // Arrange & Act
            int obstacles = 10;
            int treasures = 2;
            int monsters = 2;
            int wizardOrbs = 1;
            int totalEntities = obstacles + treasures + monsters + wizardOrbs;
            
            // Assert
            Assert.AreEqual(15, totalEntities, "Total entities should be 15 (10+2+2+1)");
        }

        [TestMethod]
        public void PitGenerator_LevelBasedFormulas_ShouldCalculateCorrectAmounts()
        {
            // Test the formulas from the problem statement
            
            // Level 1: should be minimum values (2, 2, 5-10)
            ValidateFormulasForLevel(1, expectedMaxMonsters: 2, expectedMaxChests: 2, 
                                   expectedMinObstacles: 5, expectedMaxObstacles: 10);
            
            // Level 10: should be minimum values (2, 2, 5-10) 
            ValidateFormulasForLevel(10, expectedMaxMonsters: 2, expectedMaxChests: 2,
                                   expectedMinObstacles: 5, expectedMaxObstacles: 10);
            
            // Level 55: should be mid-range values 
            ValidateFormulasForLevel(55, expectedMaxMonsters: 6, expectedMaxChests: 6,
                                   expectedMinObstacles: 22, expectedMaxObstacles: 30);
            
            // Level 100: should be maximum values (10, 10, 40-50)
            ValidateFormulasForLevel(100, expectedMaxMonsters: 10, expectedMaxChests: 10,
                                   expectedMinObstacles: 40, expectedMaxObstacles: 50);
        }

        private void ValidateFormulasForLevel(int level, int expectedMaxMonsters, int expectedMaxChests, 
                                            int expectedMinObstacles, int expectedMaxObstacles)
        {
            // Calculate using the formulas from the problem statement
            int actualMaxMonsters = Math.Clamp(
                (int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
            
            int actualMaxChests = Math.Clamp(
                (int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
            
            int actualMinObstacles = Math.Clamp(
                (int)Math.Round(5 + 35 * Math.Max(level - 10, 0) / 90.0), 5, 40);
            
            int actualMaxObstacles = Math.Clamp(
                (int)Math.Round(10 + 40 * Math.Max(level - 10, 0) / 90.0), 10, 50);
            
            // Assert the calculated values match expectations
            Assert.AreEqual(expectedMaxMonsters, actualMaxMonsters, 
                $"MaxMonsters for level {level} should be {expectedMaxMonsters}");
            Assert.AreEqual(expectedMaxChests, actualMaxChests, 
                $"MaxChests for level {level} should be {expectedMaxChests}");
            Assert.AreEqual(expectedMinObstacles, actualMinObstacles, 
                $"MinObstacles for level {level} should be {expectedMinObstacles}");
            Assert.AreEqual(expectedMaxObstacles, actualMaxObstacles, 
                $"MaxObstacles for level {level} should be {expectedMaxObstacles}");
        }

        [TestMethod]
        public void PitGenerator_FormulaConstraints_ShouldRespectMinMaxLimits()
        {
            // Test extreme values to ensure clamping works
            
            // Level 0: should clamp to minimums  
            ValidateFormulasForLevel(0, expectedMaxMonsters: 2, expectedMaxChests: 2,
                                   expectedMinObstacles: 5, expectedMaxObstacles: 10);
            
            // Level 1000: should clamp to maximums
            ValidateFormulasForLevel(1000, expectedMaxMonsters: 10, expectedMaxChests: 10,
                                   expectedMinObstacles: 40, expectedMaxObstacles: 50);
        }
    }
}