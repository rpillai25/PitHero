using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Equipment.Swords;
using RolePlayingFramework.Equipment.Armor;
using RolePlayingFramework.Equipment.Accessories;
using System.Linq;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the Second Chance Merchant Vault system.
    /// Verifies that items from deceased heroes are properly stored and stacked.
    /// </summary>
    [TestClass]
    public class SecondChanceMerchantVaultTests
    {
        private SecondChanceMerchantVault _vault;

        [TestInitialize]
        public void SetUp()
        {
            _vault = new SecondChanceMerchantVault();
        }

        [TestMethod]
        public void AddItem_SingleItem_AddsToVault()
        {
            // Arrange
            var sword = ShortSword.Create();

            // Act
            _vault.AddItem(sword);

            // Assert
            Assert.AreEqual(1, _vault.StackCount);
            Assert.AreEqual(1, _vault.TotalItemCount);
            Assert.AreEqual("ShortSword", _vault.Stacks[0].ItemTemplate.Name);
            Assert.AreEqual(1, _vault.Stacks[0].Quantity);
        }

        [TestMethod]
        public void AddItem_TwoIdenticalGear_CreatesOneStackWithQuantityTwo()
        {
            // Arrange
            var sword1 = ShortSword.Create();
            var sword2 = ShortSword.Create();

            // Act
            _vault.AddItem(sword1);
            _vault.AddItem(sword2);

            // Assert
            Assert.AreEqual(1, _vault.StackCount, "Should have only one stack for identical items");
            Assert.AreEqual(2, _vault.Stacks[0].Quantity, "Stack should contain 2 items");
        }

        [TestMethod]
        public void AddItem_DifferentGear_CreatesSeparateStacks()
        {
            // Arrange
            var sword = ShortSword.Create();
            var armor = LeatherArmor.Create();

            // Act
            _vault.AddItem(sword);
            _vault.AddItem(armor);

            // Assert
            Assert.AreEqual(2, _vault.StackCount);
            Assert.AreEqual(2, _vault.TotalItemCount);
        }

        [TestMethod]
        public void AddItem_Consumables_StacksCorrectly()
        {
            // Arrange
            var potion1 = new HPPotion();
            potion1.StackCount = 5;
            var potion2 = new HPPotion();
            potion2.StackCount = 8;

            // Act
            _vault.AddItem(potion1);
            _vault.AddItem(potion2);

            // Assert
            Assert.AreEqual(1, _vault.StackCount, "Potions should stack together");
            Assert.AreEqual(13, _vault.Stacks[0].Quantity, "Should have combined 5 + 8 = 13 potions");
        }

        [TestMethod]
        public void AddItem_ConsumablesExceedingMaxStack_CreatesMultipleStacks()
        {
            // Arrange
            var potion1 = new HPPotion();
            potion1.StackCount = 998;
            var potion2 = new HPPotion();
            potion2.StackCount = 10;

            // Act
            _vault.AddItem(potion1);
            _vault.AddItem(potion2);

            // Assert - Total is 998 + 10 = 1008 potions, which should be split as 999 + 9
            Assert.AreEqual(2, _vault.StackCount, "Should create two stacks when exceeding max");
            
            // Find the stacks (order may vary)
            var stacks = _vault.Stacks.ToList();
            var quantities = stacks.Select(s => s.Quantity).OrderBy(q => q).ToList();
            
            Assert.AreEqual(9, quantities[0], "Second stack should have remainder (1008 - 999 = 9)");
            Assert.AreEqual(999, quantities[1], "First stack should be maxed at 999");
        }

        [TestMethod]
        public void AddItem_MaxStackOf999_VerifyCapEnforced()
        {
            // Arrange
            var potion = new HPPotion();
            potion.StackCount = 999;

            // Act
            _vault.AddItem(potion);

            // Assert
            Assert.AreEqual(999, _vault.Stacks[0].Quantity);

            // Try to add one more
            var potion2 = new HPPotion();
            potion2.StackCount = 1;
            _vault.AddItem(potion2);

            // Should create a new stack
            Assert.AreEqual(2, _vault.StackCount);
            Assert.AreEqual(1, _vault.Stacks[1].Quantity);
        }

        [TestMethod]
        public void AddItems_MultipleItems_AddsAllCorrectly()
        {
            // Arrange
            var items = new IItem[]
            {
                ShortSword.Create(),
                ShortSword.Create(),
                LeatherArmor.Create(),
                new HPPotion() { StackCount = 10 },
                new HPPotion() { StackCount = 5 },
                ProtectRing.Create()
            };

            // Act
            _vault.AddItems(items);

            // Assert
            Assert.AreEqual(4, _vault.StackCount, "Should have 4 unique stacks");
            Assert.AreEqual(19, _vault.TotalItemCount, "Total: 2 swords + 1 armor + 15 potions + 1 ring = 19");
        }

        [TestMethod]
        public void RemoveQuantity_ValidRemoval_RemovesCorrectly()
        {
            // Arrange
            var potion = new HPPotion();
            potion.StackCount = 20;
            _vault.AddItem(potion);
            var stack = _vault.Stacks[0];

            // Act
            bool result = _vault.RemoveQuantity(stack, 5);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(15, stack.Quantity);
            Assert.AreEqual(1, _vault.StackCount, "Stack should still exist");
        }

        [TestMethod]
        public void RemoveQuantity_RemoveAllInStack_RemovesStack()
        {
            // Arrange
            var sword = ShortSword.Create();
            _vault.AddItem(sword);
            var stack = _vault.Stacks[0];

            // Act
            bool result = _vault.RemoveQuantity(stack, 1);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, _vault.StackCount, "Stack should be removed");
        }

        [TestMethod]
        public void RemoveQuantity_InsufficientQuantity_ReturnsFalse()
        {
            // Arrange
            var sword = ShortSword.Create();
            _vault.AddItem(sword);
            var stack = _vault.Stacks[0];

            // Act
            bool result = _vault.RemoveQuantity(stack, 5);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, stack.Quantity, "Quantity should not change");
        }

        [TestMethod]
        public void Clear_WithItems_RemovesAllItems()
        {
            // Arrange
            _vault.AddItem(ShortSword.Create());
            _vault.AddItem(LeatherArmor.Create());
            _vault.AddItem(new HPPotion());

            // Act
            _vault.Clear();

            // Assert
            Assert.AreEqual(0, _vault.StackCount);
            Assert.AreEqual(0, _vault.TotalItemCount);
        }

        [TestMethod]
        public void SimulateHeroDeath_AllEquipmentAndInventory_StacksProperly()
        {
            // Arrange - Simulate a hero with:
            // - 2 different swords in inventory
            // - 1 armor equipped
            // - 16 HP potions in one stack
            // - 16 HP potions in another stack
            // - 1 accessory
            var items = new IItem[]
            {
                ShortSword.Create(),      // Inventory
                ShortSword.Create(),      // Inventory (duplicate)
                LeatherArmor.Create(),      // Equipped armor
                new HPPotion() { StackCount = 16 },  // Inventory stack 1
                new HPPotion() { StackCount = 16 },  // Inventory stack 2
                ProtectRing.Create()      // Equipped accessory
            };

            // Act
            _vault.AddItems(items);

            // Assert
            Assert.AreEqual(4, _vault.StackCount, "Should have 4 unique item types");
            
            // Find the potion stack
            var potionStack = _vault.Stacks.FirstOrDefault(s => s.ItemTemplate.Kind == ItemKind.Consumable);
            Assert.IsNotNull(potionStack);
            Assert.AreEqual(32, potionStack.Quantity, "HP Potions should stack: 16 + 16 = 32");

            // Find the sword stack
            var swordStack = _vault.Stacks.FirstOrDefault(s => s.ItemTemplate.Name == "ShortSword");
            Assert.IsNotNull(swordStack);
            Assert.AreEqual(2, swordStack.Quantity, "Short Swords should stack: 2 total");
        }

        [TestMethod]
        public void AddItem_MultipleHeroDeaths_AccumulatesItems()
        {
            // Arrange - Simulate 3 heroes dying with HP potions
            var hero1Items = new IItem[] { new HPPotion() { StackCount = 20 } };
            var hero2Items = new IItem[] { new HPPotion() { StackCount = 30 } };
            var hero3Items = new IItem[] { new HPPotion() { StackCount = 15 } };

            // Act
            _vault.AddItems(hero1Items);
            _vault.AddItems(hero2Items);
            _vault.AddItems(hero3Items);

            // Assert
            Assert.AreEqual(1, _vault.StackCount, "Should have one stack of HP Potions");
            Assert.AreEqual(65, _vault.Stacks[0].Quantity, "Should have 20 + 30 + 15 = 65 potions");
        }

        [TestMethod]
        public void AddItem_LargeStacksAcrossMultipleHeroes_HandlesMaxStackCorrectly()
        {
            // Arrange - Simulate many heroes dying with many potions
            for (int i = 0; i < 10; i++)
            {
                var potion = new HPPotion() { StackCount = 150 };
                _vault.AddItem(potion);
            }

            // Act - Total should be 10 * 150 = 1500 potions

            // Assert
            Assert.AreEqual(2, _vault.Stacks.Count(s => s.ItemTemplate.Kind == ItemKind.Consumable), 
                "Should have 2 stacks: 999 + 501");
            
            var potionStacks = _vault.Stacks.Where(s => s.ItemTemplate.Kind == ItemKind.Consumable).ToList();
            var totalPotions = potionStacks.Sum(s => s.Quantity);
            Assert.AreEqual(1500, totalPotions, "Total potions should be 1500");
        }
    }
}

