using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;

namespace PitHero.Tests.UI
{
    [TestClass]
    public class InventorySortingTests
    {
        [TestMethod]
        public void InventoryGrid_DefaultSortOrder_ShouldBeTimeDescending()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            
            // Act
            var sortOrder = inventoryGrid.GetCurrentSortOrder();
            var sortDirection = inventoryGrid.GetCurrentSortDirection();
            
            // Assert
            Assert.AreEqual(InventorySortOrder.Time, sortOrder);
            Assert.AreEqual(SortDirection.Descending, sortDirection);
        }
        
        [TestMethod]
        public void InventoryGrid_SortInventory_CanChangeToTypeSortDescending()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            
            // Act
            inventoryGrid.SortInventory(InventorySortOrder.Type, SortDirection.Descending);
            
            // Assert
            Assert.AreEqual(InventorySortOrder.Type, inventoryGrid.GetCurrentSortOrder());
            Assert.AreEqual(SortDirection.Descending, inventoryGrid.GetCurrentSortDirection());
        }
        
        [TestMethod]
        public void InventoryGrid_SortInventory_CanChangeToNameSortAscending()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            
            // Act
            inventoryGrid.SortInventory(InventorySortOrder.Name, SortDirection.Ascending);
            
            // Assert
            Assert.AreEqual(InventorySortOrder.Name, inventoryGrid.GetCurrentSortOrder());
            Assert.AreEqual(SortDirection.Ascending, inventoryGrid.GetCurrentSortDirection());
        }
    }
}
