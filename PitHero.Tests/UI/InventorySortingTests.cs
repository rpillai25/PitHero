using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Equipment;
using System.Linq;

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
        
        [TestMethod]
        public void InventoryGrid_SortInventory_FiresOnSortOrderChangedEvent()
        {
            // Arrange
            var inventoryGrid = new InventoryGrid();
            InventorySortOrder? firedSortOrder = null;
            SortDirection? firedSortDirection = null;
            inventoryGrid.OnSortOrderChanged += (order, direction) =>
            {
                firedSortOrder = order;
                firedSortDirection = direction;
            };
            
            // Act
            inventoryGrid.SortInventory(InventorySortOrder.Type, SortDirection.Ascending);
            
            // Assert
            Assert.AreEqual(InventorySortOrder.Type, firedSortOrder);
            Assert.AreEqual(SortDirection.Ascending, firedSortDirection);
        }
    }
}
