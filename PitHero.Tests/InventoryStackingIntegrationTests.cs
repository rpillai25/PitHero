using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.Tests
{
    [TestClass]
    public class InventoryStackingIntegrationTests
    {
        [TestMethod]
        public void StackingIntegration_HeroPicksUpMultiplePotions_CreatesStacks()
        {
            // Create a bag directly (simulating what HeroComponent would have)
            var bag = new ItemBag();

            // Pick up 10 HP potions
            for (int i = 0; i < 10; i++)
            {
                var potion = PotionItems.HPPotion();
                Assert.IsTrue(bag.TryAdd(potion), $"Failed to add potion {i + 1}");
            }

            // Should have only 1 slot used with a stack of 10
            Assert.AreEqual(1, bag.Count);
            var firstItem = bag.GetSlotItem(0);
            Assert.IsNotNull(firstItem);
            Assert.IsInstanceOfType(firstItem, typeof(Consumable));
            var consumable = (Consumable)firstItem;
            Assert.AreEqual(10, consumable.StackCount);
            Assert.AreEqual("HPPotion", consumable.Name);
        }

        [TestMethod]
        public void StackingIntegration_MaxedStack_CreatesNewStack()
        {
            var bag = new ItemBag();

            // Pick up 20 HP potions (more than stack size of 16)
            for (int i = 0; i < 20; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()), $"Failed to add potion {i + 1}");
            }

            // Should have 2 slots: one maxed stack (16) and one partial (4)
            Assert.AreEqual(2, bag.Count);
            
            var firstStack = bag.GetSlotItem(0) as Consumable;
            Assert.IsNotNull(firstStack);
            Assert.AreEqual(16, firstStack.StackCount);
            
            var secondStack = bag.GetSlotItem(1) as Consumable;
            Assert.IsNotNull(secondStack);
            Assert.AreEqual(4, secondStack.StackCount);
        }

        [TestMethod]
        public void StackingIntegration_MixedItems_StacksSeparately()
        {
            var bag = new ItemBag();

            // Pick up various potions
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            }
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.MPPotion()));
            }
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.MixPotion()));
            }

            // Should have 3 stacks
            Assert.AreEqual(3, bag.Count);
            
            var hpStack = bag.GetSlotItem(0) as Consumable;
            Assert.AreEqual("HPPotion", hpStack.Name);
            Assert.AreEqual(5, hpStack.StackCount);
            
            var apStack = bag.GetSlotItem(1) as Consumable;
            Assert.AreEqual("APPotion", apStack.Name);
            Assert.AreEqual(3, apStack.StackCount);
            
            var mixStack = bag.GetSlotItem(2) as Consumable;
            Assert.AreEqual("MixPotion", mixStack.Name);
            Assert.AreEqual(2, mixStack.StackCount);
        }

        [TestMethod]
        public void StackingIntegration_TreasureDrops_AutomaticallyStack()
        {
            var bag = new ItemBag();

            // Simulate picking up treasure drops (which generate random potions)
            // For testing, we'll just add potions directly
            for (int i = 0; i < 15; i++)
            {
                var item = TreasureComponent.GenerateItemForTreasureLevel(1); // Normal potions
                if (item is Consumable)
                {
                    Assert.IsTrue(bag.TryAdd(item));
                }
            }

            // Since treasure generation is random, we can only verify that stacking occurred
            // The count should be less than 15 if any stacking happened
            Assert.IsTrue(bag.Count < 15, "Items should have stacked");
        }

        [TestMethod]
        public void StackingIntegration_BagUpgrades_DontStack()
        {
            var bag = new ItemBag();

            // Add multiple bag items
            Assert.IsTrue(bag.TryAdd(BagItems.StandardBag()));
            Assert.IsTrue(bag.TryAdd(BagItems.ForagersBag()));
            Assert.IsTrue(bag.TryAdd(BagItems.TravellersBag()));

            // Should have 3 slots - bags don't stack
            Assert.AreEqual(3, bag.Count);
            
            // Verify each bag is separate
            var bag1 = bag.GetSlotItem(0) as Consumable;
            Assert.AreEqual("Standard Bag", bag1.Name);
            Assert.AreEqual(1, bag1.StackCount);
            Assert.AreEqual(1, bag1.StackSize); // Not stackable
            
            var bag2 = bag.GetSlotItem(1) as Consumable;
            Assert.AreEqual("Forager's Bag", bag2.Name);
            Assert.AreEqual(1, bag2.StackCount);
            Assert.AreEqual(1, bag2.StackSize);
        }

        [TestMethod]
        public void StackingIntegration_ConsumeFromStack_WorksCorrectly()
        {
            var bag = new ItemBag();

            // Add a stack of 5 potions
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            }
            
            var potion = bag.GetSlotItem(0) as Consumable;
            Assert.AreEqual(5, potion.StackCount);
            Assert.AreEqual(1, bag.Count);

            // Consume 3 potions
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(bag.ConsumeFromStack(0));
            }
            
            Assert.AreEqual(2, potion.StackCount);
            Assert.AreEqual(1, bag.Count); // Still in slot

            // Consume remaining 2
            Assert.IsTrue(bag.ConsumeFromStack(0));
            Assert.IsTrue(bag.ConsumeFromStack(0));
            
            Assert.AreEqual(0, bag.Count); // Now removed from bag
        }
    }
}
