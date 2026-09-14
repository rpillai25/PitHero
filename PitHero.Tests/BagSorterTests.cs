using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>Issue #414: inventory sorting (Time / Type / Alphabetical), column-major packing, stencil cells locked.</summary>
    [TestClass]
    public class BagSorterTests
    {
        private const int Cols = InventoryGrid.BagColumns;
        private const int Rows = InventoryGrid.BagRows;

        private static Gear MakeGear(string name, ItemKind kind) =>
            new Gear(name, kind, ItemRarity.Normal, "test", 10, new StatBlock(1, 0, 0, 0));

        private static ItemBag MakeBag() => new ItemBag("Test", InventoryGrid.BagCapacity);

        private static int ColumnMajor(int n) => (n % Rows) * Cols + n / Rows;

        [TestMethod]
        public void Sort_PacksColumnByColumnTopToBottom()
        {
            var bag = MakeBag();
            var items = new List<IItem>();
            for (int i = 0; i < 6; i++)
            {
                var g = MakeGear("G" + i, ItemKind.WeaponSword);
                items.Add(g);
                bag.SetSlotItem(100 + i, g); // scattered on the far right
            }

            BagSorter.Sort(bag, InventorySortOrder.Name, null, Cols, Rows);

            Assert.AreSame(items[0], bag.GetSlotItem(0), "row 0 col 0");
            Assert.AreSame(items[1], bag.GetSlotItem(Cols), "row 1 col 0");
            Assert.AreSame(items[2], bag.GetSlotItem(2 * Cols), "row 2 col 0");
            Assert.AreSame(items[3], bag.GetSlotItem(3 * Cols), "row 3 col 0");
            Assert.AreSame(items[4], bag.GetSlotItem(1), "row 0 col 1");
            Assert.AreSame(items[5], bag.GetSlotItem(Cols + 1), "row 1 col 1");
            Assert.AreEqual(6, bag.Count);
            for (int i = 0; i < 6; i++) Assert.IsNull(bag.GetSlotItem(100 + i));
        }

        [TestMethod]
        public void Sort_Time_NewestFirst_StackGrowthCountsAsNew()
        {
            var bag = MakeBag();
            var potion = new HPPotion();
            var sword = MakeGear("Sword", ItemKind.WeaponSword);
            var helm = MakeGear("Helm", ItemKind.HatHelm);
            Assert.IsTrue(bag.TryAdd(potion));
            Assert.IsTrue(bag.TryAdd(sword));
            Assert.IsTrue(bag.TryAdd(helm));
            Assert.IsTrue(bag.TryAdd(new HPPotion()), "stacks onto the existing potion");

            BagSorter.Sort(bag, InventorySortOrder.Time, null, Cols, Rows);

            Assert.AreSame(potion, bag.GetSlotItem(ColumnMajor(0)), "the grown stack is the newest");
            Assert.AreSame(helm, bag.GetSlotItem(ColumnMajor(1)));
            Assert.AreSame(sword, bag.GetSlotItem(ColumnMajor(2)));
        }

        [TestMethod]
        public void Sort_Type_GearBeforeConsumables_InKindOrderThenName()
        {
            var bag = MakeBag();
            var potion = new HPPotion();
            var accessory = MakeGear("Ring", ItemKind.Accessory);
            var shield = MakeGear("Buckler", ItemKind.Shield);
            var swordB = MakeGear("Blade", ItemKind.WeaponSword);
            var swordA = MakeGear("Axe", ItemKind.WeaponSword);
            var mail = MakeGear("Mail", ItemKind.ArmorMail);
            bag.TryAdd(potion); bag.TryAdd(accessory); bag.TryAdd(shield);
            bag.TryAdd(swordB); bag.TryAdd(swordA); bag.TryAdd(mail);

            BagSorter.Sort(bag, InventorySortOrder.Type, null, Cols, Rows);

            var expected = new IItem[] { swordA, swordB, mail, shield, accessory, potion };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreSame(expected[i], bag.GetSlotItem(ColumnMajor(i)), "position " + i);
        }

        [TestMethod]
        public void Sort_Name_CaseInsensitive_WithAcquireTieBreak()
        {
            var bag = MakeBag();
            var zeta = MakeGear("zeta", ItemKind.Shield);
            var alphaOld = MakeGear("Alpha", ItemKind.Shield);
            var beta = MakeGear("beta", ItemKind.Accessory);
            var alphaNew = MakeGear("Alpha", ItemKind.Shield);
            bag.TryAdd(zeta); bag.TryAdd(alphaOld); bag.TryAdd(beta); bag.TryAdd(alphaNew);

            BagSorter.Sort(bag, InventorySortOrder.Name, null, Cols, Rows);

            Assert.AreSame(alphaNew, bag.GetSlotItem(ColumnMajor(0)), "same name: newest first");
            Assert.AreSame(alphaOld, bag.GetSlotItem(ColumnMajor(1)));
            Assert.AreSame(beta, bag.GetSlotItem(ColumnMajor(2)));
            Assert.AreSame(zeta, bag.GetSlotItem(ColumnMajor(3)));
        }

        [TestMethod]
        public void Sort_LockedSlots_KeepTheirItemsAndStayEmptyOtherwise()
        {
            var bag = MakeBag();
            var locked = new bool[bag.Capacity];
            locked[0] = true;          // row 0 col 0 holds a stencil item
            locked[Cols] = true;       // row 1 col 0 is an empty stencil cell

            var stencilItem = MakeGear("Zzz", ItemKind.WeaponSword);
            bag.SetSlotItem(0, stencilItem);
            var a = MakeGear("A", ItemKind.Shield);
            var b = MakeGear("B", ItemKind.Shield);
            var c = MakeGear("C", ItemKind.Shield);
            bag.SetSlotItem(50, c); bag.SetSlotItem(60, b); bag.SetSlotItem(70, a);

            BagSorter.Sort(bag, InventorySortOrder.Name, locked, Cols, Rows);

            Assert.AreSame(stencilItem, bag.GetSlotItem(0), "stencil item untouched");
            Assert.IsNull(bag.GetSlotItem(Cols), "empty stencil cell is not filled");
            Assert.AreSame(a, bag.GetSlotItem(2 * Cols));
            Assert.AreSame(b, bag.GetSlotItem(3 * Cols));
            Assert.AreSame(c, bag.GetSlotItem(1));
            Assert.AreEqual(4, bag.Count);
        }

        [TestMethod]
        public void Sort_PreservesAcquireSequences()
        {
            var bag = MakeBag();
            var first = MakeGear("First", ItemKind.Shield);
            var second = MakeGear("Second", ItemKind.Shield);
            bag.TryAdd(first); bag.TryAdd(second);
            long s1 = bag.GetAcquireSequence(first), s2 = bag.GetAcquireSequence(second);

            BagSorter.Sort(bag, InventorySortOrder.Time, null, Cols, Rows);

            Assert.AreEqual(s1, bag.GetAcquireSequence(first));
            Assert.AreEqual(s2, bag.GetAcquireSequence(second));
            Assert.IsTrue(s2 > s1);
        }

        [TestMethod]
        public void ItemBag_AcquireSequence_SurvivesReorderAndForgetsRemovedItems()
        {
            var bag = MakeBag();
            var a = MakeGear("A", ItemKind.Shield);
            var b = MakeGear("B", ItemKind.Shield);
            bag.TryAdd(a); bag.TryAdd(b);
            long sa = bag.GetAcquireSequence(a), sb = bag.GetAcquireSequence(b);

            bag.SetItemsInOrder(new List<IItem> { b, a });
            Assert.AreEqual(sa, bag.GetAcquireSequence(a));
            Assert.AreEqual(sb, bag.GetAcquireSequence(b));

            var unequipped = MakeGear("Unequipped", ItemKind.HatHelm);
            bag.SetItemsInOrder(new List<IItem> { b, a, unequipped });
            Assert.IsTrue(bag.GetAcquireSequence(unequipped) > sb, "an item entering the bag is the newest");

            bag.Remove(a);
            Assert.AreEqual(0, bag.GetAcquireSequence(a));

            bag.SetAcquireSequence(b, 1000);
            var later = MakeGear("Later", ItemKind.Shield);
            bag.TryAdd(later);
            Assert.IsTrue(bag.GetAcquireSequence(later) > 1000, "restored sequences push the counter forward");
        }

        [TestMethod]
        public void StencilBagMask_MarksEveryPlacedCell()
        {
            var svc = new GameStateService();
            // ShieldMastery: [Sword](0,0) [Shield](1,0); anchor at grid (5, BagRowStart + 2) → bag row 2
            svc.SetPlacedStencil("knight.shield_mastery", 5, InventoryGrid.BagRowStart + 2);

            var mask = new bool[InventoryGrid.BagCapacity];
            StencilBagMask.Build(svc.PlacedStencils, mask);

            int marked = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) marked++;
            Assert.AreEqual(2, marked);
            Assert.IsTrue(mask[2 * Cols + 5]);
            Assert.IsTrue(mask[2 * Cols + 6]);
        }
    }
}
