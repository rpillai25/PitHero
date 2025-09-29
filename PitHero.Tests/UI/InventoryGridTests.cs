using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests.UI
{
    [TestClass]
    public class InventoryGridTests
    {
        [TestMethod]
        public void InventoryGrid_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var inventoryGrid = new InventoryGrid();
            
            // Assert
            Assert.IsNotNull(inventoryGrid);
        }

        [TestMethod]
        public void FindNextAvailableSlot_ShouldReturnShortcutSlotFirst()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            
            // Act
            var nextSlot = inventoryGrid.FindNextAvailableSlot();
            
            // Assert
            Assert.IsNotNull(nextSlot);
            Assert.AreEqual(InventorySlotType.Shortcut, nextSlot.SlotType);
            Assert.AreEqual(0, nextSlot.X);
            Assert.AreEqual(3, nextSlot.Y);
            Assert.AreEqual(1, nextSlot.ShortcutKey);
        }

        [TestMethod]
        public void UpdateBagCapacity_ShouldLimitInventorySlots()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            
            // Act - Set capacity to only 8 (just shortcut slots)
            inventoryGrid.UpdateBagCapacity(8);
            
            // Assert - All shortcut slots should still be available, no inventory slots
            var nextSlot = inventoryGrid.FindNextAvailableSlot();
            Assert.IsNotNull(nextSlot);
            Assert.AreEqual(InventorySlotType.Shortcut, nextSlot.SlotType);
        }
    }
}