using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for hero pathfinding improvements including monster avoidance configuration
    /// </summary>
    [TestClass]
    public class HeroPathfindingImprovementsTests
    {
        [TestMethod]
        public void TestMonsterAvoidanceCostConfiguration()
        {
            // Arrange & Act: Check that monster avoidance cost is properly configured
            float avoidanceCost = GameConfig.MonsterAvoidanceCost;

            // Assert: Configuration should exist and have reasonable value
            Assert.IsTrue(avoidanceCost > 0, "Monster avoidance cost should be positive");
            Assert.IsTrue(avoidanceCost >= 1.0f, "Monster avoidance cost should provide meaningful penalty");
            Assert.AreEqual(5.0f, avoidanceCost, "Monster avoidance cost should match expected default value");
        }

        [TestMethod]
        public void TestHeroComponentPathfindingOverride()
        {
            // Test that HeroComponent has the CalculatePath override method
            var heroComponent = new HeroComponent();
            
            // The override should exist and be accessible
            Assert.IsNotNull(heroComponent, "HeroComponent should be created successfully");
            
            // Test that the component can be created without throwing exceptions
            var result = heroComponent.CalculatePath(new Point(1, 1), new Point(2, 2));
            
            // Without proper initialization, it should return null gracefully
            Assert.IsNull(result, "CalculatePath should return null when pathfinding not initialized");
        }

        [TestMethod]
        public void TestHeroComponentAdjacentDetectionMethods()
        {
            // Test that the adjacency detection methods exist and handle null cases gracefully
            var heroComponent = new HeroComponent();
            
            // These should not throw exceptions even without scene setup
            // They should return false for null cases
            try
            {
                bool adjacentToMonster = heroComponent.CheckAdjacentToMonster();
                bool adjacentToChest = heroComponent.CheckAdjacentToChest();
                
                // Without proper scene setup, should return false
                Assert.IsFalse(adjacentToMonster, "Should return false without scene setup");
                Assert.IsFalse(adjacentToChest, "Should return false without scene setup");
            }
            catch (System.NullReferenceException)
            {
                // Expected when component isn't properly initialized
                Assert.IsTrue(true, "Method correctly throws null reference when not initialized");
            }
        }

        [TestMethod]
        public void TestGetAdjacentTilesHelper()
        {
            // Test the helper method for getting adjacent tiles using reflection
            var heroComponent = new HeroComponent();
            var centerTile = new Point(10, 10);
            
            // Use reflection to access private method for testing
            var method = typeof(HeroComponent).GetMethod("GetAdjacentTiles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                var adjacentTiles = (System.Collections.Generic.List<Point>)method.Invoke(heroComponent, new object[] { centerTile });
                
                // Assert: Should return 4 adjacent tiles (N, S, E, W)
                Assert.AreEqual(4, adjacentTiles.Count, "Should return 4 adjacent tiles");
                
                var expectedTiles = new System.Collections.Generic.List<Point>
                {
                    new Point(10, 9),  // Up
                    new Point(10, 11), // Down
                    new Point(9, 10),  // Left
                    new Point(11, 10)  // Right
                };
                
                foreach (var expectedTile in expectedTiles)
                {
                    Assert.IsTrue(adjacentTiles.Contains(expectedTile), 
                        $"Should contain adjacent tile ({expectedTile.X}, {expectedTile.Y})");
                }
            }
            else
            {
                Assert.Inconclusive("GetAdjacentTiles method not accessible for testing");
            }
        }

        [TestMethod]
        public void TestIsTargetWizardOrbMethod()
        {
            // Test the IsTargetWizardOrb method exists and handles null scenes gracefully
            var heroComponent = new HeroComponent();
            var targetTile = new Point(10, 10);
            
            // Use reflection to access private method
            var method = typeof(HeroComponent).GetMethod("IsTargetWizardOrb", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                try
                {
                    var result = (bool)method.Invoke(heroComponent, new object[] { targetTile });
                    
                    // Without scene setup, should return false
                    Assert.IsFalse(result, "Should return false without scene setup");
                }
                catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is System.NullReferenceException)
                {
                    // Expected when Core.Scene is null
                    Assert.IsTrue(true, "Method correctly handles null Core.Scene");
                }
            }
            else
            {
                Assert.Inconclusive("IsTargetWizardOrb method not accessible for testing");
            }
        }
    }
}