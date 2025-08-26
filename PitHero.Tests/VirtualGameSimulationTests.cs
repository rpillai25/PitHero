using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using PitHero.AI;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the virtual game simulation system
    /// </summary>
    [TestClass]
    public class VirtualGameSimulationTests
    {
        [TestMethod]
        public void VirtualWorldState_Initialization_ShouldSetupCorrectly()
        {
            var world = new VirtualWorldState();
            
            // Test basic setup
            Assert.AreEqual(new Point(60, 25), world.WorldSizeTiles);
            Assert.AreEqual(new Point(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY), world.HeroPosition);
            Assert.AreEqual(10, world.PitLevel);
            Assert.IsFalse(world.IsWizardOrbActivated);
            Assert.IsTrue(world.WizardOrbPosition.HasValue);
            
            // Test pit bounds
            var expectedBounds = new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY, 
                                             GameConfig.PitRectWidth, GameConfig.PitRectHeight);
            Assert.AreEqual(expectedBounds, world.PitBounds);
            
            // Test fog of war is set up in pit
            bool hasFogInPit = false;
            for (int x = world.PitBounds.X + 1; x < world.PitBounds.Right - 1; x++)
            {
                for (int y = world.PitBounds.Y + 1; y < world.PitBounds.Bottom - 1; y++)
                {
                    if (world.HasFogOfWar(new Point(x, y)))
                    {
                        hasFogInPit = true;
                        break;
                    }
                }
                if (hasFogInPit) break;
            }
            Assert.IsTrue(hasFogInPit, "Pit should have fog of war initially");
        }

        [TestMethod]
        public void VirtualHero_StateManagement_ShouldWorkCorrectly()
        {
            var world = new VirtualWorldState();
            var hero = new VirtualHero(world);
            
            // Test initial state
            Assert.IsTrue(hero.PitInitialized);
            Assert.IsFalse(hero.InsidePit);
            Assert.IsFalse(hero.AdjacentToPitBoundaryFromOutside);
            
            // Test moving to pit boundary
            var pitBounds = world.PitBounds;
            var adjacentPos = new Point(pitBounds.X - 1, pitBounds.Y + 1);
            hero.MoveTo(adjacentPos);
            
            var worldState = hero.GetWorldState();
            Assert.IsTrue(worldState.ContainsKey(GoapConstants.AdjacentToPitBoundaryFromOutside));
            Assert.IsFalse(worldState.ContainsKey(GoapConstants.InsidePit));
            
            // Test moving inside pit
            var insidePos = new Point(pitBounds.X + 1, pitBounds.Y + 1);
            hero.MoveTo(insidePos);
            
            worldState = hero.GetWorldState();
            Assert.IsTrue(worldState.ContainsKey(GoapConstants.InsidePit));
            Assert.IsFalse(worldState.ContainsKey(GoapConstants.AdjacentToPitBoundaryFromOutside));
        }

        [TestMethod]
        public void VirtualWorldState_PitRegeneration_ShouldWorkForDifferentLevels()
        {
            var world = new VirtualWorldState();
            
            // Test level 10 (default width)
            world.RegeneratePit(10);
            Assert.AreEqual(10, world.PitLevel);
            Assert.AreEqual(GameConfig.PitRectWidth, world.PitBounds.Width);
            
            // Test level 40 (should be wider)
            world.RegeneratePit(40);
            Assert.AreEqual(40, world.PitLevel);
            Assert.IsTrue(world.PitBounds.Width > GameConfig.PitRectWidth, "Level 40 pit should be wider than default");
            
            // Test level 90 (maximum width)
            world.RegeneratePit(90);
            Assert.AreEqual(90, world.PitLevel);
            Assert.IsTrue(world.PitBounds.Width >= world.PitBounds.Width, "Level 90 should have maximum width");
            
            // Test that wizard orb is regenerated
            Assert.IsTrue(world.WizardOrbPosition.HasValue);
            Assert.IsFalse(world.IsWizardOrbActivated);
        }

        [TestMethod]
        public void VirtualGameSimulation_CompleteWorkflow_ShouldExecuteSuccessfully()
        {
            // Capture console output for validation
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                var simulation = new VirtualGameSimulation();
                
                // This should not throw any exceptions
                simulation.RunCompleteSimulation();
                
                var output = stringWriter.ToString();
                
                // Verify key stages were executed
                Assert.IsTrue(output.Contains("STEP 1: Generating pit at level 40"));
                Assert.IsTrue(output.Contains("STEP 2: Hero spawns and begins MoveToPitAction"));
                Assert.IsTrue(output.Contains("STEP 3: Hero jumps into pit"));
                Assert.IsTrue(output.Contains("STEP 4: Hero wanders and explores pit completely"));
                Assert.IsTrue(output.Contains("STEP 5: Execute complete wizard orb workflow"));
                
                // Verify each action was executed
                Assert.IsTrue(output.Contains("[MoveToPitAction]"));
                Assert.IsTrue(output.Contains("[JumpIntoPitAction]"));
                Assert.IsTrue(output.Contains("[WanderAction]"));
                Assert.IsTrue(output.Contains("[MoveToWizardOrbAction]"));
                Assert.IsTrue(output.Contains("[ActivateWizardOrbAction]"));
                Assert.IsTrue(output.Contains("[MovingToInsidePitEdgeAction]"));
                Assert.IsTrue(output.Contains("[JumpOutOfPitAction]"));
                Assert.IsTrue(output.Contains("[MoveToPitGenPointAction]"));
                
                // Verify completion
                Assert.IsTrue(output.Contains("Simulation Complete"));
                Assert.IsTrue(output.Contains("✓ Pit generation at level 40"));
                Assert.IsTrue(output.Contains("✓ Pit regeneration at higher level"));
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [TestMethod]
        public void VirtualHero_ExplorationStates_ShouldDetectMapExplored()
        {
            var world = new VirtualWorldState();
            var hero = new VirtualHero(world);
            
            // Initially map should not be explored (has fog)
            var initialState = hero.GetWorldState();
            Assert.IsFalse(initialState.ContainsKey(GoapConstants.MapExplored));
            
            // Clear all fog in pit manually
            var pitBounds = world.PitBounds;
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    world.ClearFogOfWar(new Point(x, y), 0);
                }
            }
            
            // Now map should be explored
            var clearedState = hero.GetWorldState();
            Assert.IsTrue(clearedState.ContainsKey(GoapConstants.MapExplored));
        }

        [TestMethod]
        public void VirtualHero_WizardOrbStates_ShouldDetectCorrectly()
        {
            var world = new VirtualWorldState();
            var hero = new VirtualHero(world);
            
            var orbPos = world.WizardOrbPosition;
            Assert.IsTrue(orbPos.HasValue);
            
            // Initially wizard orb should not be found (has fog)
            var initialState = hero.GetWorldState();
            Assert.IsFalse(initialState.ContainsKey(GoapConstants.FoundWizardOrb));
            Assert.IsFalse(initialState.ContainsKey(GoapConstants.AtWizardOrb));
            
            // Clear fog around wizard orb
            world.ClearFogOfWar(orbPos.Value, 1);
            
            // Now wizard orb should be found
            var foundState = hero.GetWorldState();
            Assert.IsTrue(foundState.ContainsKey(GoapConstants.FoundWizardOrb));
            
            // Move hero to wizard orb
            hero.MoveTo(orbPos.Value);
            
            // Now should be at wizard orb
            var atOrbState = hero.GetWorldState();
            Assert.IsTrue(atOrbState.ContainsKey(GoapConstants.AtWizardOrb));
        }

        [TestMethod]
        public void VirtualGameSimulation_PathfindingAndMovement_ShouldWorkCorrectly()
        {
            var world = new VirtualWorldState();
            var hero = new VirtualHero(world);
            
            var startPos = hero.Position;
            var targetPos = new Point(startPos.X + 5, startPos.Y + 3);
            
            // Test movement path setup
            var path = new System.Collections.Generic.List<Point>
            {
                new Point(startPos.X + 1, startPos.Y),
                new Point(startPos.X + 2, startPos.Y),
                new Point(startPos.X + 3, startPos.Y),
                new Point(startPos.X + 4, startPos.Y),
                new Point(startPos.X + 5, startPos.Y),
                new Point(startPos.X + 5, startPos.Y + 1),
                new Point(startPos.X + 5, startPos.Y + 2),
                targetPos
            };
            
            hero.SetMovementPath(path);
            Assert.IsTrue(hero.IsMoving);
            Assert.AreEqual(path.Count, hero.MovementQueue.Count);
            
            // Execute movement
            int steps = 0;
            while (!hero.ExecuteMovementStep() && steps < 20) // Safety limit
            {
                steps++;
            }
            
            Assert.IsFalse(hero.IsMoving);
            Assert.AreEqual(targetPos, hero.Position);
            Assert.AreEqual(0, hero.MovementQueue.Count);
        }

        [TestMethod]
        public void VirtualWorldState_VisualRepresentation_ShouldShowCorrectSymbols()
        {
            var world = new VirtualWorldState();
            var visual = world.GetVisualRepresentation();
            
            // Should contain hero symbol
            Assert.IsTrue(visual.Contains("H=Hero"));
            Assert.IsTrue(visual.Contains("w=Wizard Orb"));
            
            // Should show hero position
            Assert.IsTrue(visual.Contains("H"));
            
            // Should show wizard orb
            Assert.IsTrue(visual.Contains("w"));
            
            // Should show legend
            Assert.IsTrue(visual.Contains("Legend:"));
            Assert.IsTrue(visual.Contains("?=Fog"));
            
            // Should show pit level
            Assert.IsTrue(visual.Contains("Pit Level 10"));
        }

        [TestMethod]
        public void VirtualPitLevelQueue_QueueManagement_ShouldWorkCorrectly()
        {
            var queue = new VirtualPitLevelQueue();
            
            // Initially empty
            Assert.IsFalse(queue.HasQueuedLevel);
            Assert.IsNull(queue.DequeueLevel());
            
            // Queue a level
            queue.QueueLevel(50);
            Assert.IsTrue(queue.HasQueuedLevel);
            
            // Dequeue the level
            var level = queue.DequeueLevel();
            Assert.AreEqual(50, level);
            Assert.IsFalse(queue.HasQueuedLevel);
            
            // Queue is empty again
            Assert.IsNull(queue.DequeueLevel());
        }
    }
}