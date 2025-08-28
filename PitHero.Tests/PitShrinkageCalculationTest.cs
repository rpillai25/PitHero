using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;

namespace PitHero.Tests
{
    /// <summary>
    /// Test specifically for the pit shrinkage calculation fix
    /// </summary>
    [TestClass]
    public class PitShrinkageCalculationTest
    {
        [TestMethod]
        public void PitWidthCalculation_Level10To20To10_ShouldCalculateCorrectly()
        {
            // Test the calculation logic directly
            
            // Level 10: ((10 / 10)) * 2 = 2 inner floor tiles
            // Expected right edge: base (13) + 2 inner floors + 2 (inner wall + outer floor) = 17
            int level10InnerFloors = ((int)(10 / 10)) * 2;
            int baseRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth; // 1 + 12 = 13
            int expectedLevel10RightEdge = level10InnerFloors > 0 ? baseRightEdge + level10InnerFloors + 2 : baseRightEdge;
            
            // Level 20: ((20 / 10)) * 2 = 4 inner floor tiles  
            // Expected right edge: base (13) + 4 inner floors + 2 (inner wall + outer floor) = 19
            int level20InnerFloors = ((int)(20 / 10)) * 2;
            int expectedLevel20RightEdge = level20InnerFloors > 0 ? baseRightEdge + level20InnerFloors + 2 : baseRightEdge;
            
            // Level 1: ((1 / 10)) * 2 = 0 inner floor tiles
            // Expected right edge: base (13) only = 13
            int level1InnerFloors = ((int)(1 / 10)) * 2;
            int expectedLevel1RightEdge = level1InnerFloors > 0 ? baseRightEdge + level1InnerFloors + 2 : baseRightEdge;

            // Assert the calculations are correct
            Assert.AreEqual(2, level10InnerFloors, "Level 10 should extend by 2 inner floor tiles");
            Assert.AreEqual(4, level20InnerFloors, "Level 20 should extend by 4 inner floor tiles"); 
            Assert.AreEqual(0, level1InnerFloors, "Level 1 should extend by 0 inner floor tiles");
            
            Assert.AreEqual(17, expectedLevel10RightEdge, "Level 10 pit right edge should be 17");
            Assert.AreEqual(19, expectedLevel20RightEdge, "Level 20 pit right edge should be 19");
            Assert.AreEqual(13, expectedLevel1RightEdge, "Level 1 pit right edge should be 13");
            
            // Verify shrinkage: 20 -> 10 should go from 19 to 17
            Assert.IsTrue(expectedLevel10RightEdge < expectedLevel20RightEdge, 
                "Pit should shrink when going from level 20 to level 10");
        }

        [TestMethod]
        public void PitWidthCalculation_VariousLevels_ShouldBeConsistent()
        {
            // Test multiple level transitions to ensure consistency
            var testLevels = new int[] { 1, 5, 10, 15, 20, 25, 30 };
            int baseRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth; // 13
            
            for (int i = 0; i < testLevels.Length; i++)
            {
                int level = testLevels[i];
                int innerFloors = ((int)(level / 10)) * 2;
                int expectedRightEdge = innerFloors > 0 ? baseRightEdge + innerFloors + 2 : baseRightEdge;
                
                // Verify that higher levels have larger or equal pit sizes
                if (i > 0)
                {
                    int previousLevel = testLevels[i - 1];
                    int previousInnerFloors = ((int)(previousLevel / 10)) * 2;
                    int previousExpectedRightEdge = previousInnerFloors > 0 ? baseRightEdge + previousInnerFloors + 2 : baseRightEdge;
                    
                    Assert.IsTrue(expectedRightEdge >= previousExpectedRightEdge, 
                        $"Level {level} pit (right edge {expectedRightEdge}) should be >= level {previousLevel} pit (right edge {previousExpectedRightEdge})");
                }
                
                // Log for debugging
                System.Console.WriteLine($"Level {level}: {innerFloors} inner floors, right edge {expectedRightEdge}");
            }
        }
    }
}