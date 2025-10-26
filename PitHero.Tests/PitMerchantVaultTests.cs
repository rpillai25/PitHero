using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using System.Collections.Generic;

namespace PitHero.Tests
{
    [TestClass]
    public class PitMerchantVaultTests
    {
        [TestMethod]
        public void PitMerchantVault_AddItem_ShouldIncreaseCount()
        {
            // Arrange
            var vault = new PitMerchantVault();
            var item = GearItems.ShortSword();

            // Act
            vault.AddItem(item);

            // Assert
            Assert.AreEqual(1, vault.Count);
            Assert.AreEqual(item, vault.Items[0]);
        }

        [TestMethod]
        public void PitMerchantVault_AddMultipleItems_ShouldStoreAll()
        {
            // Arrange
            var vault = new PitMerchantVault();
            var item1 = GearItems.ShortSword();
            var item2 = GearItems.WoodenShield();
            var item3 = PotionItems.HPPotion();

            // Act
            vault.AddItem(item1);
            vault.AddItem(item2);
            vault.AddItem(item3);

            // Assert
            Assert.AreEqual(3, vault.Count);
        }

        [TestMethod]
        public void PitMerchantVault_AddItems_Collection_ShouldStoreAll()
        {
            // Arrange
            var vault = new PitMerchantVault();
            var items = new List<IItem>
            {
                GearItems.ShortSword(),
                GearItems.WoodenShield(),
                PotionItems.HPPotion()
            };

            // Act
            vault.AddItems(items);

            // Assert
            Assert.AreEqual(3, vault.Count);
        }

        [TestMethod]
        public void PitMerchantVault_RemoveItem_ShouldDecreaseCount()
        {
            // Arrange
            var vault = new PitMerchantVault();
            var item = GearItems.ShortSword();
            vault.AddItem(item);

            // Act
            var removed = vault.RemoveItem(item);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(0, vault.Count);
        }

        [TestMethod]
        public void PitMerchantVault_RemoveNonExistentItem_ShouldReturnFalse()
        {
            // Arrange
            var vault = new PitMerchantVault();
            var item = GearItems.ShortSword();

            // Act
            var removed = vault.RemoveItem(item);

            // Assert
            Assert.IsFalse(removed);
        }

        [TestMethod]
        public void PitMerchantVault_Clear_ShouldRemoveAllItems()
        {
            // Arrange
            var vault = new PitMerchantVault();
            vault.AddItem(GearItems.ShortSword());
            vault.AddItem(GearItems.WoodenShield());
            vault.AddItem(PotionItems.HPPotion());

            // Act
            vault.Clear();

            // Assert
            Assert.AreEqual(0, vault.Count);
        }

        [TestMethod]
        public void PitMerchantVault_AddNullItem_ShouldNotIncreaseCount()
        {
            // Arrange
            var vault = new PitMerchantVault();

            // Act
            vault.AddItem(null);

            // Assert
            Assert.AreEqual(0, vault.Count);
        }

        [TestMethod]
        public void PitMerchantVault_AddItems_WithNulls_ShouldOnlyAddNonNull()
        {
            // Arrange
            var vault = new PitMerchantVault();
            var items = new List<IItem>
            {
                GearItems.ShortSword(),
                null,
                GearItems.WoodenShield()
            };

            // Act
            vault.AddItems(items);

            // Assert
            Assert.AreEqual(2, vault.Count);
        }

        [TestMethod]
        public void PitMerchantVault_AddItems_NullCollection_ShouldNotThrow()
        {
            // Arrange
            var vault = new PitMerchantVault();

            // Act
            vault.AddItems(null);

            // Assert
            Assert.AreEqual(0, vault.Count);
        }

        [TestMethod]
        public void PitMerchantVault_Items_ShouldBeReadOnly()
        {
            // Arrange
            var vault = new PitMerchantVault();
            vault.AddItem(GearItems.ShortSword());

            // Act & Assert
            Assert.IsInstanceOfType(vault.Items, typeof(System.Collections.ObjectModel.ReadOnlyCollection<IItem>));
        }
    }
}
