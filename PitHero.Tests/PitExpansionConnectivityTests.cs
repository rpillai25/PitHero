using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using PitHero.AI;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Focused test for the specific pit expansion connectivity issue
    /// </summary>
    [TestClass]
    public class PitExpansionConnectivityTests
    {
        [TestMethod]
        public void VirtualGame_Level10PitExpansion_ShouldHaveFullConnectivity()
        {
            var world = new VirtualWorldState();
            var pathfinder = new VirtualPathfinder(world);

            // Test level 10 pit (first expansion)
            world.RegeneratePit(10);
            var pitBounds = world.PitBounds;
            
            Console.WriteLine($"Level 10 pit bounds: ({pitBounds.X},{pitBounds.Y},{pitBounds.Width},{pitBounds.Height})");
            
            // Verify pit actually expanded
            Assert.IsTrue(pitBounds.Width > GameConfig.PitRectWidth, 
                $"Level 10 pit should be wider than default. Expected > {GameConfig.PitRectWidth}, got {pitBounds.Width}");
            
            // Test pathfinding connectivity between different columns
            var leftColumn = new Point(pitBounds.X + 1, pitBounds.Y + 1); // First explorable column
            var rightColumn = new Point(pitBounds.Right - 2, pitBounds.Y + 1); // Last explorable column
            
            Console.WriteLine($"Testing path from left column ({leftColumn.X},{leftColumn.Y}) to right column ({rightColumn.X},{rightColumn.Y})");
            
            // Test that pathfinder can find a route between extreme columns
            var path = pathfinder.CalculatePath(leftColumn, rightColumn);
            
            if (path == null || path.Count == 0)
            {
                Assert.Fail($"CONNECTIVITY ISSUE: No path found between leftmost column ({leftColumn.X},{leftColumn.Y}) " +
                          $"and rightmost column ({rightColumn.X},{rightColumn.Y}) in expanded level 10 pit. " +
                          $"This reproduces the pit expansion bug where hero gets stuck in first 2 columns.");
            }
            
            Console.WriteLine($"Path found with {path.Count} steps - connectivity verified!");
            
            // Test multiple column pairs to ensure full connectivity
            var testPairs = new[]
            {
                (new Point(pitBounds.X + 1, pitBounds.Y + 2), new Point(pitBounds.X + 3, pitBounds.Y + 2)), // Columns 1-3
                (new Point(pitBounds.X + 2, pitBounds.Y + 3), new Point(pitBounds.Right - 3, pitBounds.Y + 3)), // Column 2 to second-last
                (new Point(pitBounds.X + 1, pitBounds.Y + 4), new Point(pitBounds.Right - 2, pitBounds.Y + 4)), // First to last column
            };
            
            foreach (var (start, end) in testPairs)
            {
                var testPath = pathfinder.CalculatePath(start, end);
                Assert.IsNotNull(testPath, $"Should find path from ({start.X},{start.Y}) to ({end.X},{end.Y})");
                Assert.IsTrue(testPath.Count > 0, $"Path from ({start.X},{start.Y}) to ({end.X},{end.Y}) should have steps");
                Console.WriteLine($"✓ Path from ({start.X},{start.Y}) to ({end.X},{end.Y}): {testPath.Count} steps");
            }
            
            Console.WriteLine("All connectivity tests passed - pit expansion maintains full reachability!");
        }
        
        [TestMethod]
        public void VirtualGame_NormalPitVsExpandedPit_ConnectivityComparison()
        {
            var world = new VirtualWorldState();
            var pathfinder = new VirtualPathfinder(world);

            // Test normal pit (level 5)
            world.RegeneratePit(5);
            var normalBounds = world.PitBounds;
            var normalExplorableColumns = normalBounds.Width - 2; // Exclude boundary columns
            
            Console.WriteLine($"Normal pit (level 5): {normalExplorableColumns} explorable columns");
            
            // Test expanded pit (level 10) 
            world.RegeneratePit(10);
            var expandedBounds = world.PitBounds;
            var expandedExplorableColumns = expandedBounds.Width - 2; // Exclude boundary columns
            
            Console.WriteLine($"Expanded pit (level 10): {expandedExplorableColumns} explorable columns");
            
            // Verify expansion occurred
            Assert.IsTrue(expandedExplorableColumns > normalExplorableColumns,
                $"Expanded pit should have more explorable columns. Normal: {normalExplorableColumns}, Expanded: {expandedExplorableColumns}");
            
            // Test that expanded pit can reach all columns
            var firstColumn = new Point(expandedBounds.X + 1, expandedBounds.Y + 1);
            
            for (int col = expandedBounds.X + 2; col < expandedBounds.Right - 1; col++)
            {
                var targetColumn = new Point(col, expandedBounds.Y + 1);
                var path = pathfinder.CalculatePath(firstColumn, targetColumn);
                
                Assert.IsNotNull(path, $"Should find path from column {firstColumn.X} to column {col}");
                Assert.IsTrue(path.Count > 0, $"Path to column {col} should have steps");
                
                Console.WriteLine($"✓ Column {col} reachable from column {firstColumn.X}");
            }
            
            Console.WriteLine($"SUCCESS: All {expandedExplorableColumns} columns in expanded pit are reachable!");
        }
    }
}