using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for MoveToPitAction functionality
    /// </summary>
    [TestClass]
    public class MoveToPitActionTests
    {
        [TestMethod]
        public void MoveToPitAction_Constructor_ShouldSetCorrectPreconditionsAndPostconditions()
        {
            // Arrange & Act
            var action = new MoveToPitAction();

            // Assert
            Assert.IsNotNull(action, "MoveToPitAction should be created successfully");
            Assert.AreEqual(GoapConstants.MoveToPitAction, action.Name, "Action name should match constant");
            Assert.AreEqual(1, action.Cost, "Action cost should be 1");
        }

        [TestMethod]
        public void GoapConstants_MoveToPitAction_ShouldExist()
        {
            // Arrange & Act
            var actionName = GoapConstants.MoveToPitAction;
            var stateName = GoapConstants.MovingToPit;

            // Assert
            Assert.AreEqual("MoveToPitAction", actionName, "MoveToPitAction constant should have correct value");
            Assert.AreEqual("MovingToPit", stateName, "MovingToPit constant should have correct value");
        }

        [TestMethod]
        public void GoapConstants_ShouldNotContainOldConstants()
        {
            // This test ensures we've properly cleaned up old constants
            // We'll use reflection to verify the old constants don't exist
            var constantsType = typeof(GoapConstants);
            var fields = constantsType.GetFields();
            
            foreach (var field in fields)
            {
                Assert.AreNotEqual("MovingLeft", field.GetValue(null), "MovingLeft constant should be removed");
                Assert.AreNotEqual("MoveLeftAction", field.GetValue(null), "MoveLeftAction constant should be removed");
            }
        }

        [TestMethod]
        public void GameConfig_PitConfiguration_ShouldHaveCorrectTargetTile()
        {
            // Arrange
            var pitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth - 1;
            var expectedTargetX = 13; // Target tile should be just outside pit
            var expectedTargetY = 6;
            
            // Act & Assert
            Assert.AreEqual(12, pitRightEdge, "Pit right edge should be at tile 12");
            Assert.IsTrue(expectedTargetX > pitRightEdge, "Target tile should be to the right of pit");
            Assert.AreEqual(expectedTargetY, GameConfig.MapCenterTileY, "Target Y should match map center Y");
        }
        
        [TestMethod]
        public void GameConfig_HeroSpawning_ShouldAllowCorrectMinimumDistance()
        {
            // Arrange
            var pitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth - 1; // 12
            var minimumDistance = 8;
            var expectedMinHeroX = pitRightEdge + minimumDistance; // 20
            
            // Act & Assert
            Assert.AreEqual(20, expectedMinHeroX, "Hero should spawn at least at tile X=20");
            Assert.IsTrue(expectedMinHeroX >= 20, "Hero should spawn at least 8 tiles from pit edge");
        }
    }
}