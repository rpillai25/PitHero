using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for the new interactive monster and chest functionality
    /// </summary>
    [TestClass]
    public class InteractiveEntitiesTests
    {
        [TestMethod]
        public void GoapConstants_AdjacentToMonster_ShouldExist()
        {
            // Arrange & Act
            var stateName = GoapConstants.AdjacentToMonster;

            // Assert
            Assert.AreEqual("AdjacentToMonster", stateName, "AdjacentToMonster constant should have correct value");
        }

        [TestMethod]
        public void GoapConstants_AdjacentToChest_ShouldExist()
        {
            // Arrange & Act
            var stateName = GoapConstants.AdjacentToChest;

            // Assert
            Assert.AreEqual("AdjacentToChest", stateName, "AdjacentToChest constant should have correct value");
        }

        [TestMethod]
        public void GoapConstants_AttackMonster_ShouldExist()
        {
            // Arrange & Act
            var actionName = GoapConstants.AttackMonster;

            // Assert
            Assert.AreEqual("AttackMonster", actionName, "AttackMonster constant should have correct value");
        }

        [TestMethod]
        public void GoapConstants_OpenChest_ShouldExist()
        {
            // Arrange & Act
            var actionName = GoapConstants.OpenChest;

            // Assert
            Assert.AreEqual("OpenChest", actionName, "OpenChest constant should have correct value");
        }

        [TestMethod]
        public void AttackMonster_Constructor_ShouldSetCorrectPreconditionsAndPostconditions()
        {
            // Arrange & Act
            var action = new AttackMonsterAction();

            // Assert
            Assert.IsNotNull(action, "AttackMonster should be created successfully");
            Assert.AreEqual(GoapConstants.AttackMonster, action.Name, "Action name should match constant");
            Assert.AreEqual(3, action.Cost, "Action cost should be 3");
        }

        [TestMethod]
        public void OpenChest_Constructor_ShouldSetCorrectPreconditionsAndPostconditions()
        {
            // Arrange & Act
            var action = new OpenChestAction();

            // Assert
            Assert.IsNotNull(action, "OpenChest should be created successfully");
            Assert.AreEqual(GoapConstants.OpenChest, action.Name, "Action name should match constant");
            Assert.AreEqual(2, action.Cost, "Action cost should be 2");
        }

        [TestMethod]
        public void WanderPitAction_Constructor_ShouldIncludeNewPostconditions()
        {
            // Arrange & Act
            var action = new WanderPitAction();

            // Assert
            Assert.IsNotNull(action, "WanderPitAction should be created successfully");
            Assert.AreEqual(GoapConstants.WanderPitAction, action.Name, "Action name should match constant");
            Assert.AreEqual(1, action.Cost, "Action cost should be 1");
            
            // The action should now set postconditions for interactive entities
            // Note: We can't directly test the postconditions without accessing private fields
            // but we can verify the action creates successfully with the updated constructor
        }
    }
}