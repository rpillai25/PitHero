using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.Tests
{
    [TestClass]
    public class ConsumableStackingTests
    {
        [TestMethod]
        public void Consumable_HasDefaultStackSize_Of16()
        {
            var potion = PotionItems.HPPotion();
            Assert.AreEqual(16, potion.StackSize);
        }

        [TestMethod]
        public void Consumable_HasDefaultStackCount_Of1()
        {
            var potion = PotionItems.HPPotion();
            Assert.AreEqual(1, potion.StackCount);
        }

        [TestMethod]
        public void ItemBag_StacksSameConsumables()
        {
            var bag = new ItemBag("Test Bag", 12);
            var potion1 = PotionItems.HPPotion();
            var potion2 = PotionItems.HPPotion();

            // Add first potion
            Assert.IsTrue(bag.TryAdd(potion1));
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(1, potion1.StackCount);

            // Add second potion of same type - should stack
            Assert.IsTrue(bag.TryAdd(potion2));
            Assert.AreEqual(1, bag.Count); // Still only 1 slot used
            Assert.AreEqual(2, potion1.StackCount); // Stack count increased
        }

        [TestMethod]
        public void ItemBag_DoesNotStackDifferentConsumables()
        {
            var bag = new ItemBag("Test Bag", 12);
            var hpPotion = PotionItems.HPPotion();
            var apPotion = PotionItems.APPotion();

            // Add HP potion
            Assert.IsTrue(bag.TryAdd(hpPotion));
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(1, hpPotion.StackCount);

            // Add AP potion - should NOT stack (different item)
            Assert.IsTrue(bag.TryAdd(apPotion));
            Assert.AreEqual(2, bag.Count); // 2 slots used
            Assert.AreEqual(1, hpPotion.StackCount);
            Assert.AreEqual(1, apPotion.StackCount);
        }

        [TestMethod]
        public void ItemBag_StartsNewStackWhenMaxed()
        {
            var bag = new ItemBag("Test Bag", 12);
            var firstPotion = PotionItems.HPPotion();

            // Add first potion
            Assert.IsTrue(bag.TryAdd(firstPotion));
            Assert.AreEqual(1, bag.Count);

            // Add 15 more to max out the stack
            for (int i = 0; i < 15; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            }
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(16, firstPotion.StackCount);

            // Add one more - should create a new stack
            var newPotion = PotionItems.HPPotion();
            Assert.IsTrue(bag.TryAdd(newPotion));
            Assert.AreEqual(2, bag.Count); // Now 2 slots used
            Assert.AreEqual(16, firstPotion.StackCount); // First stack still maxed
            Assert.AreEqual(1, newPotion.StackCount); // New stack started
        }

        [TestMethod]
        public void ItemBag_FindsPartialStackBeforeCreatingNew()
        {
            var bag = new ItemBag("Test Bag", 12);
            
            // Create first HP stack with 5 items
            var firstPotion = PotionItems.HPPotion();
            Assert.IsTrue(bag.TryAdd(firstPotion));
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            }
            Assert.AreEqual(5, firstPotion.StackCount);
            Assert.AreEqual(1, bag.Count); // 1 slot: HP(5)

            // Add different item
            var apPotion = PotionItems.APPotion();
            Assert.IsTrue(bag.TryAdd(apPotion));
            Assert.AreEqual(2, bag.Count); // 2 slots: HP(5), AP(1)

            // Max out the first HP stack
            for (int i = 0; i < 11; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            }
            Assert.AreEqual(16, firstPotion.StackCount);
            Assert.AreEqual(2, bag.Count); // Still 2 slots: HP(16), AP(1)

            // Add another HP potion - should create new stack since first is maxed
            var secondPotion = PotionItems.HPPotion();
            Assert.IsTrue(bag.TryAdd(secondPotion));
            Assert.AreEqual(3, bag.Count); // 3 slots: HP(16), AP(1), HP(1)
            Assert.AreEqual(16, firstPotion.StackCount);
            Assert.AreEqual(1, secondPotion.StackCount);

            // Add more HP potions to the second stack
            for (int i = 0; i < 9; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            }
            Assert.AreEqual(10, secondPotion.StackCount);
            Assert.AreEqual(3, bag.Count); // Still 3 slots: HP(16), AP(1), HP(10)

            // Add one more HP - should go to second partial stack (10/16)
            Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            Assert.AreEqual(3, bag.Count); // Still 3 slots
            Assert.AreEqual(16, firstPotion.StackCount); // First stack still maxed
            Assert.AreEqual(11, secondPotion.StackCount); // Second stack increased
        }

        [TestMethod]
        public void ItemBag_DoesNotStackNonConsumables()
        {
            var bag = new ItemBag("Test Bag", 12);
            var bag1 = BagItems.StandardBag();
            var bag2 = BagItems.StandardBag();

            // Verify bags are consumables but have StackSize of 1
            Assert.AreEqual(1, bag1.StackSize);
            Assert.AreEqual(1, bag2.StackSize);

            // Add first bag
            Assert.IsTrue(bag.TryAdd(bag1));
            Assert.AreEqual(1, bag.Count);

            // Add second bag - should NOT stack (StackSize is 1)
            Assert.IsTrue(bag.TryAdd(bag2));
            Assert.AreEqual(2, bag.Count); // 2 slots used
        }

        [TestMethod]
        public void Consumable_StackCount_CanBeModified()
        {
            var potion = PotionItems.HPPotion();
            Assert.AreEqual(1, potion.StackCount);

            potion.StackCount = 5;
            Assert.AreEqual(5, potion.StackCount);

            potion.StackCount = 16;
            Assert.AreEqual(16, potion.StackCount);
        }

        [TestMethod]
        public void ItemBag_ConsumeFromStack_DecrementsCount()
        {
            var bag = new ItemBag("Test Bag", 12);
            var potion = PotionItems.HPPotion();

            // Add 5 potions to create a stack
            Assert.IsTrue(bag.TryAdd(potion));
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(bag.TryAdd(PotionItems.HPPotion()));
            }
            Assert.AreEqual(5, potion.StackCount);
            Assert.AreEqual(1, bag.Count);

            // Consume one from the stack
            Assert.IsTrue(bag.ConsumeFromStack(0));
            Assert.AreEqual(4, potion.StackCount);
            Assert.AreEqual(1, bag.Count); // Still 1 slot used

            // Consume 3 more
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(bag.ConsumeFromStack(0));
            }
            Assert.AreEqual(1, potion.StackCount);
            Assert.AreEqual(1, bag.Count);

            // Consume the last one - should remove the item
            Assert.IsTrue(bag.ConsumeFromStack(0));
            Assert.AreEqual(0, potion.StackCount);
            Assert.AreEqual(0, bag.Count); // Now empty
        }

        [TestMethod]
        public void ItemBag_ConsumeFromStack_HandlesNonStackableConsumable()
        {
            var bag = new ItemBag("Test Bag", 12);
            var bagItem = BagItems.StandardBag();

            Assert.IsTrue(bag.TryAdd(bagItem));
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(1, bagItem.StackCount);

            // Consume - should remove the bag item since StackSize is 1
            Assert.IsTrue(bag.ConsumeFromStack(0));
            Assert.AreEqual(0, bagItem.StackCount);
            Assert.AreEqual(0, bag.Count); // Item removed
        }
    }
}
