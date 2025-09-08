using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using Microsoft.Xna.Framework;
using System;

namespace PitHero.Tests
{
    [TestClass]
    public class ExplorationDebugTests
    {
        [TestMethod]
        public void DebugLevel10ExpansionConnectivity()
        {
            var world = new VirtualWorldState();
            var hero = new VirtualHero(world);
            var stateMachine = new VirtualHeroStateMachine(hero, world);

            Console.WriteLine("=== Debugging Level 10 Expanded Pit Connectivity ===");

            // Test level 10 - should have expanded pit
            world.RegeneratePit(10);
            var pitBounds = world.PitBounds;

            Console.WriteLine($"Level 10 pit bounds: {pitBounds}");
            Console.WriteLine($"Expected columns: {pitBounds.Width - 2} (excluding boundary columns)");

            // Reset hero
            hero.ExploredPit = false;
            hero.InsidePit = false;
            stateMachine.ResetFailedTargets();

            // Place hero outside pit
            hero.TeleportTo(new Point(pitBounds.X - 2, pitBounds.Y + pitBounds.Height / 2));

            Console.WriteLine($"Hero starting position: {hero.Position}");
            Console.WriteLine("Initial world state:");
            Console.WriteLine(world.GetVisualRepresentation());

            // Track reachable columns during exploration
            var reachableColumns = new System.Collections.Generic.HashSet<int>();
            int maxTicks = 200;
            
            for (int i = 0; i < maxTicks && !stateMachine.IsExplorationComplete(); i++)
            {
                var beforePos = hero.Position;
                stateMachine.Update();
                var afterPos = hero.Position;
                
                // Track reachable columns
                if (pitBounds.Contains(afterPos))
                {
                    reachableColumns.Add(afterPos.X);
                }
                
                if (i % 20 == 0)
                {
                    Console.WriteLine($"Tick {i}: Hero at {afterPos}, Columns reached: {reachableColumns.Count}, Fog: {CountFog(world)}");
                }
            }

            Console.WriteLine($"\nFinal exploration results:");
            Console.WriteLine($"Exploration complete: {stateMachine.IsExplorationComplete()}");
            Console.WriteLine($"Reachable columns: {reachableColumns.Count}/{pitBounds.Width - 2}");
            Console.WriteLine($"Columns reached: [{string.Join(", ", reachableColumns)}]");
            
            var expectedColumns = new System.Collections.Generic.List<int>();
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                expectedColumns.Add(x);
            }
            Console.WriteLine($"Expected columns: [{string.Join(", ", expectedColumns)}]");
            
            // Show final world state
            Console.WriteLine("\nFinal world state:");
            Console.WriteLine(world.GetVisualRepresentation());
            
            // Find which columns are missing
            var missingColumns = new System.Collections.Generic.List<int>();
            foreach (var col in expectedColumns)
            {
                if (!reachableColumns.Contains(col))
                    missingColumns.Add(col);
            }
            
            if (missingColumns.Count > 0)
            {
                Console.WriteLine($"Missing columns: [{string.Join(", ", missingColumns)}]");
                
                // Check if missing columns have passable tiles
                foreach (var col in missingColumns)
                {
                    Console.WriteLine($"Analyzing column {col}:");
                    for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                    {
                        var tile = new Point(col, y);
                        bool passable = !world.IsCollisionTile(tile);
                        bool hasFog = world.HasFogOfWar(tile);
                        Console.WriteLine($"  ({col},{y}): Passable={passable}, HasFog={hasFog}");
                    }
                }
            }
            
            // This test should pass to verify the fix
            Assert.IsTrue(stateMachine.IsExplorationComplete(), "Exploration should complete");
            Assert.AreEqual(expectedColumns.Count, reachableColumns.Count, "Should reach all expected columns");
        }

        private int CountFog(VirtualWorldState world)
        {
            var bounds = world.PitBounds;
            int count = 0;
            for (int x = bounds.X + 1; x < bounds.Right - 1; x++)
            {
                for (int y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
                {
                    if (world.HasFogOfWar(new Point(x, y)))
                        count++;
                }
            }
            return count;
        }
    }
}