using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using PitHero.ECS.Components;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroStateMachineTests
    {
        [TestMethod]
        public void HeroStateMachine_Constructor_ShouldInitializeWithoutErrors()
        {
            // Arrange & Act
            var stateMachine = new HeroStateMachine();

            // Assert
            Assert.IsNotNull(stateMachine, "HeroStateMachine should be created successfully");
        }

        [TestMethod]
        public void HeroStateMachine_Interfaces_ShouldImplementRequired()
        {
            // Arrange
            var stateMachine = new HeroStateMachine();

            // Assert
            Assert.IsTrue(stateMachine is Nez.Component, "HeroStateMachine should inherit from Nez.Component");
            Assert.IsTrue(stateMachine is Nez.IUpdatable, "HeroStateMachine should implement IUpdatable");
            Assert.IsTrue(stateMachine is IPausableComponent, "HeroStateMachine should implement IPausableComponent");
            Assert.IsTrue(stateMachine.ShouldPause, "HeroStateMachine should respect pause state");
        }

        [TestMethod]
        public void HeroState_Enum_ShouldHaveExpectedValues()
        {
            // Assert - Verify all expected states exist
            Assert.IsTrue(System.Enum.IsDefined(typeof(HeroState), HeroState.Idle), "Idle state should be defined");
            Assert.IsTrue(System.Enum.IsDefined(typeof(HeroState), HeroState.GoTo), "GoTo state should be defined");
            Assert.IsTrue(System.Enum.IsDefined(typeof(HeroState), HeroState.PerformAction), "PerformAction state should be defined");
            
            // Verify enum values
            Assert.AreEqual(0, (int)HeroState.Idle, "Idle should be the first state (0)");
            Assert.AreEqual(1, (int)HeroState.GoTo, "GoTo should be the second state (1)");
            Assert.AreEqual(2, (int)HeroState.PerformAction, "PerformAction should be the third state (2)");
        }
    }
}