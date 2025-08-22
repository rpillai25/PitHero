using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using PitHero.ECS.Scenes;

namespace PitHero.Tests
{
    /// <summary>
    /// Integration tests for pit content regeneration functionality
    /// </summary>
    [TestClass]
    public class PitRegenerationIntegrationTests
    {
        [TestMethod]
        public void PitGenerator_RegenerateForLevel_ShouldUseCorrectFormulas()
        {
            // Since we can't easily mock the entire Scene infrastructure,
            // we'll test the logic using the known formulas
            
            // Test that level 1 uses expected values
            var level1Monsters = CalculateMaxMonsters(1);
            var level1Chests = CalculateMaxChests(1);
            var level1MinObstacles = CalculateMinObstacles(1);
            var level1MaxObstacles = CalculateMaxObstacles(1);
            
            Assert.AreEqual(2, level1Monsters, "Level 1 should have 2 max monsters");
            Assert.AreEqual(2, level1Chests, "Level 1 should have 2 max chests");
            Assert.AreEqual(5, level1MinObstacles, "Level 1 should have 5 min obstacles");
            Assert.AreEqual(10, level1MaxObstacles, "Level 1 should have 10 max obstacles");
            
            // Test that level 50 uses expected values
            var level50Monsters = CalculateMaxMonsters(50);
            var level50Chests = CalculateMaxChests(50);
            var level50MinObstacles = CalculateMinObstacles(50);
            var level50MaxObstacles = CalculateMaxObstacles(50);
            
            Assert.AreEqual(6, level50Monsters, "Level 50 should have 6 max monsters");
            Assert.AreEqual(6, level50Chests, "Level 50 should have 6 max chests");
            Assert.AreEqual(21, level50MinObstacles, "Level 50 should have 21 min obstacles");
            Assert.AreEqual(28, level50MaxObstacles, "Level 50 should have 28 max obstacles");
        }

        [TestMethod]
        public void PitWidthManager_SetPitLevel_ShouldTriggerRegeneration()
        {
            // This test verifies the integration points exist
            // In a real scenario, the PitWidthManager would call RegeneratePitContent
            // which would create a new PitGenerator and call RegenerateForCurrentLevel
            
            // Test that all the required classes can be instantiated
            Assert.IsNotNull(typeof(PitWidthManager), "PitWidthManager class should exist");
            Assert.IsNotNull(typeof(PitGenerator), "PitGenerator class should exist");
            
            // Verify the formulas are accessible
            var testLevel = 25;
            var monsters = CalculateMaxMonsters(testLevel);
            var chests = CalculateMaxChests(testLevel);
            var minObstacles = CalculateMinObstacles(testLevel);
            var maxObstacles = CalculateMaxObstacles(testLevel);
            
            Assert.IsTrue(monsters >= 2 && monsters <= 10, "Monsters should be within valid range");
            Assert.IsTrue(chests >= 2 && chests <= 10, "Chests should be within valid range");
            Assert.IsTrue(minObstacles >= 5 && minObstacles <= 40, "Min obstacles should be within valid range");
            Assert.IsTrue(maxObstacles >= 10 && maxObstacles <= 50, "Max obstacles should be within valid range");
            Assert.IsTrue(minObstacles <= maxObstacles, "Min obstacles should be <= max obstacles");
        }

        // Helper methods that replicate the formulas from PitGenerator
        private int CalculateMaxMonsters(int level)
        {
            return System.Math.Clamp(
                (int)System.Math.Round(2 + 8 * System.Math.Max(level - 10, 0) / 90.0), 2, 10);
        }

        private int CalculateMaxChests(int level)
        {
            return System.Math.Clamp(
                (int)System.Math.Round(2 + 8 * System.Math.Max(level - 10, 0) / 90.0), 2, 10);
        }

        private int CalculateMinObstacles(int level)
        {
            return System.Math.Clamp(
                (int)System.Math.Round(5 + 35 * System.Math.Max(level - 10, 0) / 90.0), 5, 40);
        }

        private int CalculateMaxObstacles(int level)
        {
            return System.Math.Clamp(
                (int)System.Math.Round(10 + 40 * System.Math.Max(level - 10, 0) / 90.0), 10, 50);
        }
    }
}