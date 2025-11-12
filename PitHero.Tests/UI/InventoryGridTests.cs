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
        public void FindNextAvailableSlot_ShouldReturnInventorySlot()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            
            // Act
            var nextSlot = inventoryGrid.FindNextAvailableSlot();
            
            // Assert - Should return first inventory slot which is (0,0) in the new 20x6 layout
            Assert.IsNotNull(nextSlot);
            Assert.AreEqual(InventorySlotType.Inventory, nextSlot.SlotType);
        }

        [TestMethod]
        public void UpdateBagCapacity_NoLongerLimitsSlots()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            
            // Act - UpdateBagCapacity no longer limits slots (fixed 120 capacity)
            inventoryGrid.UpdateBagCapacity(8);
            
            // Assert - Should still find inventory slots as capacity is fixed
            var nextSlot = inventoryGrid.FindNextAvailableSlot();
            Assert.IsNotNull(nextSlot);
            Assert.AreEqual(InventorySlotType.Inventory, nextSlot.SlotType);
        }
    }
}