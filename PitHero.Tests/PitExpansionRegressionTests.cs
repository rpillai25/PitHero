using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using PitHero.AI;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Focused regression test for pit expansion connectivity issues
    /// </summary>
    [TestClass]
    public class PitExpansionRegressionTests
    {
        [TestMethod]
        public void VirtualGame_PitExpansionLevels1Through9To10_ShouldExploreFullyWithoutGettingStuck()
        {
            // Capture console output for debugging
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            try
            {
                var world = new VirtualWorldState();
                var hero = new VirtualHero(world);
                var stateMachine = new VirtualHeroStateMachine(hero, world);

                // Step 1: Simulate progression through levels 1-9 (normal pit width)
                Console.WriteLine("=== Testing Pit Levels 1-9 (Normal Width) ===");
                for (int level = 1; level <= 9; level++)
                {
                    Console.WriteLine($"\n--- Testing Level {level} ---");
                    
                    // Regenerate pit for current level
                    world.RegeneratePit(level);
                    
                    // Reset hero and state machine for new level
                    hero.ExploredPit = false;
                    hero.InsidePit = false;
                    stateMachine.ResetFailedTargets();
                    
                    // Place hero outside pit
                    var pitBounds = world.PitBounds;
                    hero.TeleportTo(new Point(pitBounds.X - 2, pitBounds.Y + pitBounds.Height / 2));
                    
                    // Execute state machine to explore pit
                    int maxTicks = 1000; // Higher limit for debugging
                    int tickCount = 0;
                    
                    while (!stateMachine.IsExplorationComplete() && tickCount < maxTicks)
                    {
                        stateMachine.Update();
                        tickCount++;
                    }
                    
                    // Verify exploration completed successfully
                    Assert.IsTrue(stateMachine.IsExplorationComplete(), 
                        $"Level {level}: Exploration should complete successfully. Took {tickCount} ticks.");
                    
                    // Verify pit width hasn't expanded yet (levels 1-9)
                    Assert.AreEqual(GameConfig.PitRectWidth, pitBounds.Width, 
                        $"Level {level}: Pit width should still be default ({GameConfig.PitRectWidth})");
                        
                    Console.WriteLine($"Level {level}: SUCCESS - Exploration completed in {tickCount} ticks");
                }

                // Step 2: Test the critical level 10 expansion
                Console.WriteLine("\n=== Testing Level 10 (Expanded Pit) ===");
                
                world.RegeneratePit(10);
                var expandedPitBounds = world.PitBounds;
                
                // Verify pit actually expanded
                Assert.IsTrue(expandedPitBounds.Width > GameConfig.PitRectWidth,
                    $"Level 10 pit should be wider than default. Expected > {GameConfig.PitRectWidth}, got {expandedPitBounds.Width}");
                
                Console.WriteLine($"Level 10: Pit expanded from {GameConfig.PitRectWidth} to {expandedPitBounds.Width} tiles wide");
                
                // Reset hero for level 10 test
                hero.ExploredPit = false;
                hero.InsidePit = false;
                stateMachine.ResetFailedTargets();
                
                // Place hero outside expanded pit
                hero.TeleportTo(new Point(expandedPitBounds.X - 2, expandedPitBounds.Y + expandedPitBounds.Height / 2));
                
                // Test: Jump into pit
                Console.WriteLine("Jumping into expanded pit...");
                
                // Simulate JumpIntoPitAction
                var insidePos = new Point(expandedPitBounds.X + 1, expandedPitBounds.Y + 1);
                hero.TeleportTo(insidePos);
                hero.InsidePit = true;
                
                Console.WriteLine($"Hero jumped into expanded pit at ({hero.Position.X}, {hero.Position.Y})");
                
                // Test: Attempt to explore ALL columns of expanded pit
                Console.WriteLine("Starting exploration of expanded pit...");
                
                int maxExpandedTicks = 2000; // Higher limit for expanded pit
                int expandedTickCount = 0;
                
                // Track which columns hero can reach
                var reachableColumns = new System.Collections.Generic.HashSet<int>();
                
                while (!stateMachine.IsExplorationComplete() && expandedTickCount < maxExpandedTicks)
                {
                    var beforePos = hero.Position;
                    stateMachine.Update();
                    var afterPos = hero.Position;
                    
                    // Track reachable columns
                    if (expandedPitBounds.Contains(afterPos))
                    {
                        reachableColumns.Add(afterPos.X);
                    }
                    
                    expandedTickCount++;
                    
                    // Log progress every 100 ticks
                    if (expandedTickCount % 100 == 0)
                    {
                        var fogCount = CountRemainingFog(world);
                        var columnCount = reachableColumns.Count;
                        var expectedColumns = expandedPitBounds.Width - 2; // Exclude boundary columns
                        
                        Console.WriteLine($"Tick {expandedTickCount}: Hero at ({afterPos.X},{afterPos.Y}), " +
                                        $"Fog remaining: {fogCount}, Reachable columns: {columnCount}/{expectedColumns}");
                    }
                }
                
                // Analyze results
                var finalFogCount = CountRemainingFog(world);
                var finalColumnCount = reachableColumns.Count;
                var expectedColumnCount = expandedPitBounds.Width - 2; // Exclude left and right boundary columns
                
                Console.WriteLine($"\n=== Level 10 Results ===");
                Console.WriteLine($"Exploration completed: {stateMachine.IsExplorationComplete()}");
                Console.WriteLine($"Ticks taken: {expandedTickCount}/{maxExpandedTicks}");
                Console.WriteLine($"Fog remaining: {finalFogCount}");
                Console.WriteLine($"Reachable columns: {finalColumnCount}/{expectedColumnCount}");
                Console.WriteLine($"Columns reached: [{string.Join(", ", reachableColumns)}]");
                Console.WriteLine($"Expected columns: [{string.Join(", ", GetExpectedColumns(expandedPitBounds))}]");
                
                // CRITICAL ASSERTIONS - These detect the pit expansion bug
                
                if (!stateMachine.IsExplorationComplete())
                {
                    // This indicates the bug - hero got stuck and couldn't explore fully
                    Assert.Fail($"REGRESSION DETECTED: Hero got stuck exploring level 10 expanded pit. " +
                              $"Only reached {finalColumnCount}/{expectedColumnCount} columns. " +
                              $"This indicates the pit expansion broke connectivity/reachability. " +
                              $"Fog remaining: {finalFogCount}, Ticks: {expandedTickCount}");
                }
                
                // Verify hero can reach a reasonable portion of columns (connectivity check)
                // If hero can reach at least 33% of columns and fog is cleared, connectivity is good
                var minimumExpectedColumns = Math.Max(1, expectedColumnCount / 3);
                Assert.IsTrue(finalColumnCount >= minimumExpectedColumns,
                    $"Hero should be able to reach at least {minimumExpectedColumns} out of {expectedColumnCount} explorable columns " +
                    $"to prove connectivity, but only reached {finalColumnCount}. This indicates serious connectivity issues.");
                
                // More strict check: if fog is fully cleared, connectivity is proven regardless of column visits
                // The hero's 2-tile radius fog clearing means it can reach all areas effectively
                if (finalFogCount == 0)
                {
                    Console.WriteLine($"Level 10: Full fog clearance achieved, proving pit connectivity " +
                                    $"(reached {finalColumnCount}/{expectedColumnCount} columns directly)");
                }
                else
                {
                    // If fog isn't fully cleared, then column coverage becomes more important
                    Assert.IsTrue(finalColumnCount >= expectedColumnCount * 0.5,
                        $"With incomplete fog clearance ({finalFogCount} remaining), hero should reach at least " +
                        $"50% of columns ({expectedColumnCount * 0.5:F0}) but only reached {finalColumnCount}.");
                }
                
                // Verify exploration actually completed
                Assert.IsTrue(stateMachine.IsExplorationComplete(),
                    "Exploration should complete successfully in expanded pit");
                
                Assert.AreEqual(0, finalFogCount,
                    "All fog should be cleared if exploration completed successfully");
                
                Console.WriteLine("Level 10: SUCCESS - Full exploration of expanded pit completed!");
                
            }
            finally
            {
                Console.SetOut(originalOut);
                
                // Print captured output for debugging
                var output = stringWriter.ToString();
                if (output.Contains("REGRESSION DETECTED") || output.Contains("ERROR"))
                {
                    Console.WriteLine("=== CAPTURED OUTPUT (for debugging) ===");
                    Console.WriteLine(output);
                }
            }
        }

        /// <summary>
        /// Count remaining fog tiles in pit
        /// </summary>
        private int CountRemainingFog(VirtualWorldState world)
        {
            var pitBounds = world.PitBounds;
            int count = 0;
            
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    if (world.HasFogOfWar(new Point(x, y)))
                        count++;
                }
            }
            
            return count;
        }

        /// <summary>
        /// Get expected explorable columns for pit bounds
        /// </summary>
        private System.Collections.Generic.List<int> GetExpectedColumns(Rectangle pitBounds)
        {
            var columns = new System.Collections.Generic.List<int>();
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                columns.Add(x);
            }
            return columns;
        }
    }
}