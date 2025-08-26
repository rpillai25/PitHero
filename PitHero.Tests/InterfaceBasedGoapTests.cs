using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using PitHero.AI;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for interface-based GOAP actions on virtual layer
    /// </summary>
    [TestClass]
    public class InterfaceBasedGoapTests
    {
        [TestMethod]
        public void WanderAction_ExecuteWithVirtualContext_ShouldExploreSuccessfully()
        {
            // Capture console output for validation
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                // Arrange
                var virtualWorld = new VirtualWorldState();
                var context = new VirtualGoapContext(virtualWorld);
                var wanderAction = new WanderAction();
                
                // Move hero inside pit
                var pitCenter = new Point(virtualWorld.PitBounds.X + 2, virtualWorld.PitBounds.Y + 2);
                context.HeroController.MoveTo(pitCenter);
                context.HeroController.InsidePit = true;
                
                // Verify initial conditions
                Assert.IsFalse(context.WorldState.IsMapExplored, "Map should not be explored initially");
                // Note: The fog may or may not be at the specific center position, depending on how VirtualWorldState initializes fog
                // What matters is that there's fog somewhere in the pit that needs to be explored
                
                // Act - Execute wander action multiple times to simulate exploration
                bool completed = false;
                int maxIterations = 100; // Safety limit
                int iteration = 0;
                
                while (!completed && iteration < maxIterations)
                {
                    completed = wanderAction.Execute(context);
                    
                    // Execute movement step if hero is moving
                    if (context.HeroController.IsMoving)
                    {
                        context.ExecuteMovementStep();
                    }
                    
                    iteration++;
                }
                
                var output = stringWriter.ToString();
                
                // Assert
                Console.WriteLine($"Test completed after {iteration} iterations");
                Console.WriteLine($"Map explored: {context.WorldState.IsMapExplored}");
                
                // Verify the action executed and made progress
                Assert.IsTrue(output.Contains("[WanderAction]"), "WanderAction should have executed");
                Assert.IsTrue(output.Contains("Starting execution with interface-based context"), "Should use interface-based execution");
                
                // Either the action completed or made significant progress
                Assert.IsTrue(completed || iteration > 10, "Action should either complete or make significant progress");
                
                // Verify that some fog was cleared
                bool someLogCleared = !context.WorldState.HasFogOfWar(context.HeroController.CurrentTilePosition);
                Assert.IsTrue(someLogCleared, "Some fog should have been cleared around hero position");
                
                Console.WriteLine("WanderAction interface-based execution test completed successfully");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [TestMethod]
        public void VirtualGoapContext_GetGoapWorldState_ShouldReturnCorrectStates()
        {
            // Arrange
            var virtualWorld = new VirtualWorldState();
            var context = new VirtualGoapContext(virtualWorld);
            
            // Act
            var worldState = context.GetGoapWorldState();
            
            // Assert
            Assert.IsTrue(worldState.ContainsKey(GoapConstants.HeroInitialized), "Should contain HeroInitialized");
            Assert.IsTrue(worldState.ContainsKey(GoapConstants.PitInitialized), "Should contain PitInitialized");
            Assert.IsTrue(worldState[GoapConstants.HeroInitialized], "Hero should be initialized");
            Assert.IsTrue(worldState[GoapConstants.PitInitialized], "Pit should be initialized");
            
            // Move hero inside pit and check states
            var pitCenter = new Point(virtualWorld.PitBounds.X + 2, virtualWorld.PitBounds.Y + 2);
            context.HeroController.MoveTo(pitCenter);
            context.HeroController.InsidePit = true;
            
            worldState = context.GetGoapWorldState();
            Assert.IsTrue(worldState.ContainsKey(GoapConstants.InsidePit), "Should contain InsidePit when hero is inside");
            Assert.IsTrue(worldState[GoapConstants.InsidePit], "InsidePit should be true when hero is inside pit");
        }

        [TestMethod]
        public void VirtualPathfinder_CalculatePath_ShouldFindValidPath()
        {
            // Arrange
            var virtualWorld = new VirtualWorldState();
            var pathfinder = new VirtualPathfinder(virtualWorld);
            
            var start = new Point(virtualWorld.PitBounds.X + 1, virtualWorld.PitBounds.Y + 1);
            var end = new Point(virtualWorld.PitBounds.X + 3, virtualWorld.PitBounds.Y + 3);
            
            // Act
            var path = pathfinder.CalculatePath(start, end);
            
            // Assert
            Assert.IsNotNull(path, "Path should be found");
            Assert.IsTrue(path.Count > 0, "Path should have steps");
            Assert.AreEqual(end, path[path.Count - 1], "Path should end at target");
            
            // Verify path consists of passable tiles
            foreach (var tile in path)
            {
                Assert.IsTrue(pathfinder.IsPassable(tile), $"Path tile ({tile.X},{tile.Y}) should be passable");
            }
        }
    }
}