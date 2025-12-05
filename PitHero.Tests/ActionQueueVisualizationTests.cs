using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using PitHero.AI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;

namespace PitHero.Tests
{
    [TestClass]
    public class ActionQueueVisualizationTests
    {
        [TestMethod]
        public void ActionQueueVisualization_CanBeCreated()
        {
            // Arrange & Act
            var visualization = new ActionQueueVisualizationComponent();
            
            // Assert
            Assert.IsNotNull(visualization);
            Assert.IsTrue(visualization.Width > 0);
            Assert.IsTrue(visualization.Height > 0);
        }
        
        [TestMethod]
        public void ActionQueueVisualization_HeightMatchesMaxQueueSize()
        {
            // Arrange
            var visualization = new ActionQueueVisualizationComponent();
            
            // Act
            float expectedHeight = 32 * ActionQueue.MaxQueueSize + 2 * (ActionQueue.MaxQueueSize - 1);
            
            // Assert
            Assert.AreEqual(expectedHeight, visualization.Height);
        }
    }
}
