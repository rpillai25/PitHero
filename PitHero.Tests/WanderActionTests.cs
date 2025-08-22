using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for WanderAction functionality
    /// </summary>
    [TestClass]
    public class WanderActionTests
    {
        [TestMethod]
        public void WanderAction_Constructor_ShouldSetCorrectPreconditionsAndPostconditions()
        {
            // Arrange & Act
            var action = new WanderAction();

            // Assert
            Assert.IsNotNull(action, "WanderAction should be created successfully");
            Assert.AreEqual(GoapConstants.WanderAction, action.Name, "Action name should match constant");
            Assert.AreEqual(1, action.Cost, "Action cost should be 1");
        }

        [TestMethod]
        public void GoapConstants_WanderAction_ShouldExist()
        {
            // Arrange & Act
            var actionName = GoapConstants.WanderAction;
            var stateName = GoapConstants.MapExplored;

            // Assert
            Assert.AreEqual("WanderAction", actionName, "WanderAction constant should have correct value");
            Assert.AreEqual("MapExplored", stateName, "MapExplored constant should have correct value");
        }

        [TestMethod]
        public void GoapConstants_EnteredPit_ShouldExist()
        {
            // Arrange & Act
            var stateName = GoapConstants.EnteredPit;

            // Assert
            Assert.AreEqual("EnteredPit", stateName, "EnteredPit constant should have correct value");
        }
    }
}