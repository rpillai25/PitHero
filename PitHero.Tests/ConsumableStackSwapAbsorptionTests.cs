using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.Tests
{
    [TestClass]
    public class ConsumableStackSwapAbsorptionTests
    {
        [TestMethod]
        public void ItemBag_SwapSlots_FullAbsorption_SourceAbsorbedIntoTarget()
        {
            // Source Item Slot = Stack of 9 HPPotion
            // Target Item Slot = 1 HPPotion
            // Result = Target Item Slot has 10 HPPotion. Source Item Slot is cleared.
            var bag = new ItemBag("Test Bag", 12);
            var sourcePotion = PotionItems.HPPotion();
            sourcePotion.StackCount = 9;
            var targetPotion = PotionItems.HPPotion();
            targetPotion.StackCount = 1;

            bag.SetSlotItem(0, sourcePotion);
            bag.SetSlotItem(1, targetPotion);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // After swap: target slot (1) should have 10, source slot (0) should be null
            Assert.IsNull(bag.GetSlotItem(0), "Source slot should be cleared after full absorption");
            var resultTarget = bag.GetSlotItem(1) as Consumable;
            Assert.IsNotNull(resultTarget, "Target slot should have a consumable");
            Assert.AreEqual(10, resultTarget.StackCount, "Target stack should be 10 after absorption");
            Assert.AreEqual(1, bag.Count, "Bag should only have 1 item after full absorption");
        }

        [TestMethod]
        public void ItemBag_SwapSlots_PartialAbsorption_TargetReachesMax()
        {
            // Source Item Slot = Stack of 3 HPPotion
            // Target Item Slot = Stack of 15 HPPotion
            // Result = Target Item Slot has 16 HPPotion. Source Item Slot has 2 HPPotion.
            var bag = new ItemBag("Test Bag", 12);
            var sourcePotion = PotionItems.HPPotion();
            sourcePotion.StackCount = 3;
            var targetPotion = PotionItems.HPPotion();
            targetPotion.StackCount = 15;

            bag.SetSlotItem(0, sourcePotion);
            bag.SetSlotItem(1, targetPotion);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // After swap: target slot (1) should be at max (16), source slot (0) should have 2
            var resultSource = bag.GetSlotItem(0) as Consumable;
            var resultTarget = bag.GetSlotItem(1) as Consumable;
            
            Assert.IsNotNull(resultSource, "Source slot should still have a consumable");
            Assert.IsNotNull(resultTarget, "Target slot should have a consumable");
            Assert.AreEqual(2, resultSource.StackCount, "Source stack should be reduced to 2");
            Assert.AreEqual(16, resultTarget.StackCount, "Target stack should be at max (16)");
            Assert.AreEqual(2, bag.Count, "Bag should have 2 items after partial absorption");
        }

        [TestMethod]
        public void ItemBag_SwapSlots_ReversePartialAbsorption()
        {
            // Source Item Slot = Stack of 15 HPPotion
            // Target Item Slot = Stack of 3 HPPotion
            // Result = Target Item Slot has 16 HPPotion. Source Item Slot has 2 HPPotion (13 absorbed)
            var bag = new ItemBag("Test Bag", 12);
            var sourcePotion = PotionItems.HPPotion();
            sourcePotion.StackCount = 15;
            var targetPotion = PotionItems.HPPotion();
            targetPotion.StackCount = 3;

            bag.SetSlotItem(0, sourcePotion);
            bag.SetSlotItem(1, targetPotion);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // After swap: target slot (1) should be at max (16), source slot (0) should have 2
            var resultSource = bag.GetSlotItem(0) as Consumable;
            var resultTarget = bag.GetSlotItem(1) as Consumable;
            
            Assert.IsNotNull(resultSource, "Source slot should still have a consumable");
            Assert.IsNotNull(resultTarget, "Target slot should have a consumable");
            Assert.AreEqual(2, resultSource.StackCount, "Source stack should be reduced to 2 (15-13)");
            Assert.AreEqual(16, resultTarget.StackCount, "Target stack should be at max (3+13=16)");
            Assert.AreEqual(2, bag.Count, "Bag should have 2 items after partial absorption");
        }

        [TestMethod]
        public void ItemBag_SwapSlots_DifferentConsumables_NoAbsorption()
        {
            // Different consumable types should not absorb
            var bag = new ItemBag("Test Bag", 12);
            var hpPotion = PotionItems.HPPotion();
            hpPotion.StackCount = 5;
            var apPotion = PotionItems.MPPotion();
            apPotion.StackCount = 3;

            bag.SetSlotItem(0, hpPotion);
            bag.SetSlotItem(1, apPotion);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // Should be a regular swap, no absorption
            var slot0Item = bag.GetSlotItem(0) as Consumable;
            var slot1Item = bag.GetSlotItem(1) as Consumable;
            
            Assert.IsNotNull(slot0Item);
            Assert.IsNotNull(slot1Item);
            Assert.AreEqual("AP Potion", slot0Item.Name);
            Assert.AreEqual(3, slot0Item.StackCount);
            Assert.AreEqual("HP Potion", slot1Item.Name);
            Assert.AreEqual(5, slot1Item.StackCount);
            Assert.AreEqual(2, bag.Count);
        }

        [TestMethod]
        public void ItemBag_SwapSlots_OneNull_NoAbsorption()
        {
            // One slot empty should result in a regular swap
            var bag = new ItemBag("Test Bag", 12);
            var potion = PotionItems.HPPotion();
            potion.StackCount = 5;

            bag.SetSlotItem(0, potion);
            // Slot 1 is null

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // Should be a regular swap
            Assert.IsNull(bag.GetSlotItem(0));
            var slot1Item = bag.GetSlotItem(1) as Consumable;
            Assert.IsNotNull(slot1Item);
            Assert.AreEqual(5, slot1Item.StackCount);
            Assert.AreEqual(1, bag.Count);
        }

        [TestMethod]
        public void ItemBag_SwapSlots_NonConsumable_NoAbsorption()
        {
            // Non-consumable items should not absorb
            var bag = new ItemBag("Test Bag", 12);
            var bagItem1 = BagItems.StandardBag();
            var bagItem2 = BagItems.StandardBag();

            bag.SetSlotItem(0, bagItem1);
            bag.SetSlotItem(1, bagItem2);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // Should be a regular swap, no absorption
            Assert.AreSame(bagItem2, bag.GetSlotItem(0));
            Assert.AreSame(bagItem1, bag.GetSlotItem(1));
            Assert.AreEqual(2, bag.Count);
        }

        [TestMethod]
        public void ItemBag_SwapSlots_BothAtMaxStack_NoAbsorption()
        {
            // Both stacks at max should result in a regular swap
            var bag = new ItemBag("Test Bag", 12);
            var potion1 = PotionItems.HPPotion();
            potion1.StackCount = 16;
            var potion2 = PotionItems.HPPotion();
            potion2.StackCount = 16;

            bag.SetSlotItem(0, potion1);
            bag.SetSlotItem(1, potion2);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // Should be a regular swap
            var slot0Item = bag.GetSlotItem(0) as Consumable;
            var slot1Item = bag.GetSlotItem(1) as Consumable;
            Assert.IsNotNull(slot0Item);
            Assert.IsNotNull(slot1Item);
            Assert.AreEqual(16, slot0Item.StackCount);
            Assert.AreEqual(16, slot1Item.StackCount);
            Assert.AreEqual(2, bag.Count);
        }

        [TestMethod]
        public void ItemBag_SwapSlots_SourceAndTargetSameSlot_NoChange()
        {
            // Swapping with itself should do nothing
            var bag = new ItemBag("Test Bag", 12);
            var potion = PotionItems.HPPotion();
            potion.StackCount = 5;

            bag.SetSlotItem(0, potion);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 0));

            // Should be unchanged
            var slot0Item = bag.GetSlotItem(0) as Consumable;
            Assert.IsNotNull(slot0Item);
            Assert.AreEqual(5, slot0Item.StackCount);
            Assert.AreEqual(1, bag.Count);
        }

        [TestMethod]
        public void ItemBag_SwapSlots_MixedConsumableAndNonConsumable_NoAbsorption()
        {
            // Swapping consumable with non-consumable should not absorb
            var bag = new ItemBag("Test Bag", 12);
            var potion = PotionItems.HPPotion();
            potion.StackCount = 5;
            var bagItem = BagItems.StandardBag();

            bag.SetSlotItem(0, potion);
            bag.SetSlotItem(1, bagItem);

            // Perform swap
            Assert.IsTrue(bag.SwapSlots(0, 1));

            // Should be a regular swap
            Assert.AreSame(bagItem, bag.GetSlotItem(0));
            var slot1Item = bag.GetSlotItem(1) as Consumable;
            Assert.IsNotNull(slot1Item);
            Assert.AreEqual(5, slot1Item.StackCount);
            Assert.AreEqual(2, bag.Count);
        }
    }
}
