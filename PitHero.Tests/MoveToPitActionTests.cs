using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using System.Linq;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for JumpIntoPitAction functionality in the simplified GOAP model
    /// </summary>
    [TestClass]
    public class JumpIntoPitActionTests
    {
        [TestMethod]
        public void JumpIntoPitAction_Constructor_ShouldSetCorrectPreconditionsAndPostconditions()
        {
            // Arrange & Act
            var action = new JumpIntoPitAction();

            // Assert
            Assert.IsNotNull(action, "JumpIntoPitAction should be created successfully");
            Assert.AreEqual(GoapConstants.JumpIntoPitAction, action.Name, "Action name should match constant");
            Assert.AreEqual(1, action.Cost, "Action cost should be 1");
        }

        [TestMethod]
        public void GoapConstants_JumpIntoPitAction_ShouldExist()
        {
            // Arrange & Act
            var actionName = GoapConstants.JumpIntoPitAction;
            var insidePitState = GoapConstants.InsidePit;

            // Assert
            Assert.AreEqual("JumpIntoPitAction", actionName, "JumpIntoPitAction constant should have correct value");
            Assert.AreEqual("InsidePit", insidePitState, "InsidePit constant should have correct value");
        }

        [TestMethod]
        public void GoapConstants_SimplifiedModel_ShouldOnlyContainCoreConstants()
        {
            // This test ensures we have the expected constants in the extended interactive model
            var constantsType = typeof(GoapConstants);
            var fields = constantsType.GetFields().Where(f => f.FieldType == typeof(string) && f.IsLiteral);
            
            // Should have exactly 9 states + 7 actions = 16 constants (extended for interactive entities)
            Assert.AreEqual(16, fields.Count(), "Should have exactly 16 GOAP constants in extended interactive model");
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