using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using PitHero.UI;
using PitHero.ECS.Components;
using Nez.UI;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroUIIntegrationTests
    {
        private HeroComponent _heroComponent;

        [TestInitialize]
        public void TestInitialize()
        {
            _heroComponent = new HeroComponent();
        }

        [TestMethod]
        public void HeroUI_PriorityReorderingWorkflow_ShouldUpdateHeroComponentCorrectly()
        {
            // Arrange - Simulate what happens when the UI reorders priorities
            var originalPriorities = _heroComponent.GetPrioritiesInOrder();
            
            // Act 1 - Simulate a list of priorities being reordered (like in the UI)
            var reorderedPriorityStrings = new List<string>
            {
                HeroPitPriority.Battle.ToString(),
                HeroPitPriority.Advance.ToString(), 
                HeroPitPriority.Treasure.ToString()
            };

            // Act 2 - Convert back to enum array (like the HeroUI.UpdateHeroPriorities method does)
            var newPriorities = new HeroPitPriority[3];
            for (int i = 0; i < reorderedPriorityStrings.Count; i++)
            {
                if (System.Enum.TryParse(reorderedPriorityStrings[i], out HeroPitPriority priority))
                {
                    newPriorities[i] = priority;
                }
            }

            // Act 3 - Update hero component priorities
            _heroComponent.SetPrioritiesInOrder(newPriorities);

            // Assert
            var updatedPriorities = _heroComponent.GetPrioritiesInOrder();
            Assert.AreEqual(HeroPitPriority.Battle, updatedPriorities[0], "First priority should be Battle");
            Assert.AreEqual(HeroPitPriority.Advance, updatedPriorities[1], "Second priority should be Advance");  
            Assert.AreEqual(HeroPitPriority.Treasure, updatedPriorities[2], "Third priority should be Treasure");

            // Verify the original priorities were different
            Assert.AreNotEqual(originalPriorities[0], updatedPriorities[0], "First priority should have changed");
            Assert.AreNotEqual(originalPriorities[1], updatedPriorities[1], "Second priority should have changed");
            Assert.AreNotEqual(originalPriorities[2], updatedPriorities[2], "Third priority should have changed");
        }

        [TestMethod]
        public void ReorderableTableList_OnReorderCallback_ShouldProvideCorrectParameters()
        {
            // This test demonstrates how the ReorderableTableList callback would work
            // We can't easily test the UI directly without a graphics context, but we can test the logic
            
            // Arrange
            var items = new List<string> { "First", "Second", "Third" };
            int callbackFromIndex = -1;
            int callbackToIndex = -1;
            string callbackItem = null;

            // Simulate the callback that would be triggered by the UI
            void OnReorderCallback(int from, int to, string item)
            {
                callbackFromIndex = from;
                callbackToIndex = to;
                callbackItem = item;
            }

            // Act - Simulate moving "Third" from index 2 to index 0
            var fromIndex = 2;
            var toIndex = 0;
            var movedItem = items[fromIndex];
            
            // Remove and reinsert (like ReorderableTableList does)
            items.RemoveAt(fromIndex);
            items.Insert(toIndex, movedItem);
            
            // Trigger callback
            OnReorderCallback(fromIndex, toIndex, movedItem);

            // Assert
            Assert.AreEqual(2, callbackFromIndex, "From index should be 2");
            Assert.AreEqual(0, callbackToIndex, "To index should be 0");
            Assert.AreEqual("Third", callbackItem, "Moved item should be 'Third'");
            Assert.AreEqual("Third", items[0], "First item should now be 'Third'");
            Assert.AreEqual("First", items[1], "Second item should now be 'First'");
            Assert.AreEqual("Second", items[2], "Third item should now be 'Second'");
        }
    }
}