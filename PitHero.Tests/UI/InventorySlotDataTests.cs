using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests.UI
{
    [TestClass]
    public class InventorySlotDataTests
    {
        [TestMethod]
        public void InventorySlotData_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var slotData = new InventorySlotData(2, 3, InventorySlotType.Inventory);
            
            // Assert
            Assert.AreEqual(2, slotData.X);
            Assert.AreEqual(3, slotData.Y);
            Assert.AreEqual(InventorySlotType.Inventory, slotData.SlotType);
            Assert.IsNull(slotData.EquipmentSlot);
            Assert.IsNull(slotData.ShortcutKey);
            Assert.IsNull(slotData.Item);
            Assert.IsFalse(slotData.IsHighlighted);
            Assert.IsFalse(slotData.IsHovered);
        }

        [TestMethod]
        public void InventorySlotData_EquipmentSlot_ShouldSetCorrectly()
        {
            // Arrange & Act
            var slotData = new InventorySlotData(1, 1, InventorySlotType.Equipment)
            {
                EquipmentSlot = EquipmentSlot.WeaponShield1
            };
            
            // Assert
            Assert.AreEqual(EquipmentSlot.WeaponShield1, slotData.EquipmentSlot);
        }

        [TestMethod]
        public void InventorySlotData_ShortcutKey_ShouldSetCorrectly()
        {
            // Arrange & Act
            var slotData = new InventorySlotData(0, 3, InventorySlotType.Shortcut)
            {
                ShortcutKey = 1
            };
            
            // Assert
            Assert.AreEqual(1, slotData.ShortcutKey);
        }
    }
}