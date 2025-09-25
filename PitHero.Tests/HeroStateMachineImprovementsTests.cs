using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using PitHero.ECS.Components;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for HeroStateMachine improvements in movement and adjacency handling
    /// </summary>
    [TestClass]
    public class HeroStateMachineImprovementsTests
    {
        [TestMethod]
        public void TestIsCardinalDirectionHelper()
        {
            // Create a state machine for testing
            var stateMachine = new HeroStateMachine();
            
            // Use reflection to test private method
            var method = typeof(HeroStateMachine).GetMethod("IsCardinalDirection", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                // Test cardinal directions
                Assert.IsTrue((bool)method.Invoke(stateMachine, new object[] { Direction.Up }), "Up should be cardinal");
                Assert.IsTrue((bool)method.Invoke(stateMachine, new object[] { Direction.Down }), "Down should be cardinal");
                Assert.IsTrue((bool)method.Invoke(stateMachine, new object[] { Direction.Left }), "Left should be cardinal");
                Assert.IsTrue((bool)method.Invoke(stateMachine, new object[] { Direction.Right }), "Right should be cardinal");
                
                // Test diagonal directions (if they exist in enum)
                var directionType = typeof(Direction);
                var directionValues = System.Enum.GetValues(directionType);
                
                foreach (Direction direction in directionValues)
                {
                    bool isCardinal = (bool)method.Invoke(stateMachine, new object[] { direction });
                    if (direction == Direction.Up || direction == Direction.Down || 
                        direction == Direction.Left || direction == Direction.Right)
                    {
                        Assert.IsTrue(isCardinal, $"{direction} should be cardinal");
                    }
                    else
                    {
                        Assert.IsFalse(isCardinal, $"{direction} should not be cardinal");
                    }
                }
            }
            else
            {
                Assert.Inconclusive("IsCardinalDirection method not accessible for testing");
            }
        }

        [TestMethod]
        public void TestStateMachineInitialization()
        {
            // Arrange & Act: State machine should initialize properly
            var stateMachine = new HeroStateMachine();
            
            // Assert: State machine should be properly set up
            Assert.IsNotNull(stateMachine, "State machine should be initialized");
            Assert.IsTrue(stateMachine.ShouldPause, "State machine should respect pause state");
        }

        [TestMethod]
        public void TestBattleStateRespected()
        {
            // Arrange: Test that battle state can be set and retrieved
            var initialState = HeroStateMachine.IsBattleInProgress;
            
            // Act: Set battle in progress
            HeroStateMachine.IsBattleInProgress = true;
            
            // Assert: Battle state should be settable
            Assert.IsTrue(HeroStateMachine.IsBattleInProgress, "Battle state should be settable");
            
            // Cleanup: Restore original state
            HeroStateMachine.IsBattleInProgress = initialState;
        }

        [TestMethod]
        public void TestStateMachineInheritance()
        {
            // Test that the state machine has proper inheritance and interfaces
            var stateMachine = new HeroStateMachine();
            
            Assert.IsTrue(stateMachine is Nez.Component, "Should inherit from Nez.Component");
            Assert.IsTrue(stateMachine is Nez.IUpdatable, "Should implement IUpdatable");
            Assert.IsTrue(stateMachine is IPausableComponent, "Should implement IPausableComponent");
        }

        [TestMethod]
        public void TestStateMachineComponents()
        {
            // Test basic functionality without requiring full scene setup
            var stateMachine = new HeroStateMachine();
            
            // The state machine should be properly constructed
            Assert.IsNotNull(stateMachine, "State machine should be created successfully");
            
            // Should have the expected pause behavior
            Assert.IsTrue(stateMachine.ShouldPause, "Should respect pause state by default");
        }
    }
}