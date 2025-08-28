using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using PitHero.AI;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Debug tests for VirtualHeroStateMachine
    /// </summary>
    [TestClass]
    public class VirtualHeroStateMachineDebugTests
    {
        [TestMethod]
        public void VirtualStateMachine_BasicWanderTest_ShouldExploreSmallArea()
        {
            var world = new VirtualWorldState();
            var hero = new VirtualHero(world);
            var stateMachine = new VirtualHeroStateMachine(hero, world);

            // Start with level 1 pit
            world.RegeneratePit(1);
            var pitBounds = world.PitBounds;
            
            Console.WriteLine($"Pit bounds: ({pitBounds.X},{pitBounds.Y},{pitBounds.Width},{pitBounds.Height})");
            
            // Place hero outside pit
            hero.TeleportTo(new Point(pitBounds.X - 2, pitBounds.Y + 1));
            Console.WriteLine($"Hero initial position: ({hero.Position.X},{hero.Position.Y})");
            
            // Test state transitions manually
            for (int tick = 0; tick < 50; tick++)
            {
                var worldState = hero.GetWorldState();
                Console.WriteLine($"\nTick {tick}:");
                Console.WriteLine($"  State: {stateMachine.CurrentState}");
                Console.WriteLine($"  Hero at: ({hero.Position.X},{hero.Position.Y})");
                Console.WriteLine($"  World states: [{string.Join(", ", worldState.Keys)}]");
                
                stateMachine.Update();
                
                if (stateMachine.IsExplorationComplete())
                {
                    Console.WriteLine($"Exploration completed at tick {tick}!");
                    break;
                }
            }
            
            // Check final state
            Assert.IsTrue(stateMachine.IsExplorationComplete() || hero.GetWorldState().ContainsKey(GoapConstants.InsidePit), 
                "Should either complete exploration or at least get inside pit");
        }
        
        [TestMethod]
        public void VirtualStateMachine_JumpIntoPitTest_ShouldGetInside()
        {
            var world = new VirtualWorldState();
            var hero = new VirtualHero(world);
            var stateMachine = new VirtualHeroStateMachine(hero, world);

            // Start with level 1 pit
            world.RegeneratePit(1);
            var pitBounds = world.PitBounds;
            
            // Place hero outside pit
            hero.TeleportTo(new Point(pitBounds.X - 1, pitBounds.Y + 1));
            
            Console.WriteLine($"Before: Hero at ({hero.Position.X},{hero.Position.Y}), InsidePit: {hero.InsidePit}");
            
            // Execute a few state machine updates
            for (int i = 0; i < 10; i++)
            {
                stateMachine.Update();
                var worldState = hero.GetWorldState();
                Console.WriteLine($"Tick {i}: State={stateMachine.CurrentState}, Position=({hero.Position.X},{hero.Position.Y}), States=[{string.Join(", ", worldState.Keys)}]");
                
                if (worldState.ContainsKey(GoapConstants.InsidePit))
                {
                    Console.WriteLine("Successfully got inside pit!");
                    break;
                }
            }
            
            // Should be inside pit
            Assert.IsTrue(hero.GetWorldState().ContainsKey(GoapConstants.InsidePit), "Hero should be inside pit");
        }
    }
}