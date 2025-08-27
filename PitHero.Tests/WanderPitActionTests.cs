using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for WanderPitAction functionality
    /// </summary>
    [TestClass]
    public class WanderPitActionTests
    {
        [TestMethod]
        public void WanderPitAction_Constructor_ShouldSetCorrectPreconditionsAndPostconditions()
        {
            // Arrange & Act
            var action = new WanderPitAction();

            // Assert
            Assert.IsNotNull(action, "WanderPitAction should be created successfully");
            Assert.AreEqual(GoapConstants.WanderPitAction, action.Name, "Action name should match constant");
            Assert.AreEqual(1, action.Cost, "Action cost should be 1");
        }

        [TestMethod]
        public void GoapConstants_WanderPitAction_ShouldExist()
        {
            // Arrange & Act
            var actionName = GoapConstants.WanderPitAction;
            var stateName = GoapConstants.ExploredPit;

            // Assert
            Assert.AreEqual("WanderPitAction", actionName, "WanderPitAction constant should have correct value");
            Assert.AreEqual("ExploredPit", stateName, "ExploredPit constant should have correct value");
        }

        [TestMethod]
        public void GoapConstants_InsidePit_ShouldExist()
        {
            // Arrange & Act
            var stateName = GoapConstants.InsidePit;

            // Assert
            Assert.AreEqual("InsidePit", stateName, "InsidePit constant should have correct value");
        }
    }
}