using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Services;
using PitHero.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Synergies;
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

        private static int CountLocked(bool[] mask)
        {
            int marked = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) marked++;
            return marked;
        }

        [TestMethod]
        public void BagSortLockMask_LocksEveryPlacedStencilCell()
        {
            var svc = new GameStateService();
            // ShieldMastery: [Sword](0,0) [Shield](1,0); anchor at grid (5, BagRowStart + 2) → bag row 2
            svc.SetPlacedStencil("knight.shield_mastery", 5, InventoryGrid.BagRowStart + 2);

            var mask = new bool[InventoryGrid.BagCapacity];
            BagSortLockMask.AddPlacedStencils(svc.PlacedStencils, mask);

            Assert.AreEqual(2, CountLocked(mask));
            Assert.IsTrue(mask[2 * Cols + 5]);
            Assert.IsTrue(mask[2 * Cols + 6]);
        }

        /// <summary>Synergy whose cells are given as (bag row, bag column) pairs.</summary>
        private static ActiveSynergy MakeSynergy(params (int row, int col)[] cells)
        {
            var points = new List<Point>();
            for (int i = 0; i < cells.Length; i++) points.Add(new Point(cells[i].col, InventoryGrid.BagRowStart + cells[i].row));
            return new ActiveSynergy(SynergyPatternRegistry.GetById("knight.shield_mastery"), points[0], points);
        }

        [TestMethod]
        public void BagSortLockMask_SynergyTouchingEquipmentRow_IsLocked()
        {
            var synergies = new List<ActiveSynergy>
            {
                // One cell in bag row 1, one in an equipment row (not a bag slot)
                new ActiveSynergy(SynergyPatternRegistry.GetById("knight.shield_mastery"), new Point(10, InventoryGrid.BagRowStart + 1),
                    new List<Point> { new Point(10, InventoryGrid.BagRowStart + 1), new Point(11, InventoryGrid.BagRowStart - 1) }),
            };

            var mask = new bool[InventoryGrid.BagCapacity];
            var groups = BagSortLockMask.AddActiveSynergies(synergies, mask);

            Assert.AreEqual(0, groups.Count, "cannot move a synergy that uses an equipment slot");
            Assert.AreEqual(1, CountLocked(mask));
            Assert.IsTrue(mask[1 * Cols + 10]);
        }

        [TestMethod]
        public void BagSortLockMask_SynergyOnStencilCell_IsLocked_UnboundIsMovable()
        {
            var svc = new GameStateService();
            svc.SetPlacedStencil("knight.shield_mastery", 5, InventoryGrid.BagRowStart);  // bag row 0, cols 5-6
            var mask = new bool[InventoryGrid.BagCapacity];
            BagSortLockMask.AddPlacedStencils(svc.PlacedStencils, mask);

            var synergies = new List<ActiveSynergy>
            {
                MakeSynergy((0, 6), (1, 6)),    // shares a stencil cell: bound
                MakeSynergy((3, 20), (3, 21)),  // unbound
            };
            var groups = BagSortLockMask.AddActiveSynergies(synergies, mask);

            Assert.AreEqual(1, groups.Count);
            CollectionAssert.AreEqual(new[] { 3 * Cols + 20, 3 * Cols + 21 }, new List<int>(groups[0]));
            Assert.IsTrue(mask[1 * Cols + 6], "the bound synergy's other cell is locked too");
        }

        [TestMethod]
        public void BagSortLockMask_OverlappingSynergies_FormOneGroup()
        {
            var synergies = new List<ActiveSynergy>
            {
                MakeSynergy((0, 10), (0, 11)),
                MakeSynergy((0, 11), (1, 11)),
                MakeSynergy((2, 25), (2, 26)),
            };
            var groups = BagSortLockMask.AddActiveSynergies(synergies, new bool[InventoryGrid.BagCapacity]);

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEqual(new[] { 10, 11, Cols + 11 }, new List<int>(groups[0]));
        }

        [TestMethod]
        public void Sort_UnboundSynergy_MovesIntactRightOfSortedItems()
        {
            var bag = MakeBag();
            // L-shaped synergy blocking the leftmost columns
            var s1 = MakeGear("S1", ItemKind.WeaponSword);
            var s2 = MakeGear("S2", ItemKind.Shield);
            var s3 = MakeGear("S3", ItemKind.HatHelm);
            bag.SetSlotItem(0 * Cols + 0, s1);
            bag.SetSlotItem(1 * Cols + 0, s2);
            bag.SetSlotItem(1 * Cols + 1, s3);
            // Five loose items on the far right
            var loose = new List<IItem>();
            for (int i = 0; i < 5; i++)
            {
                var g = MakeGear("L" + i, ItemKind.Accessory);
                loose.Add(g);
                bag.SetSlotItem(Cols - 1 - i, g);
            }

            var mask = new bool[bag.Capacity];
            var groups = BagSortLockMask.AddActiveSynergies(new List<ActiveSynergy> { MakeSynergy((0, 0), (1, 0), (1, 1)) }, mask);
            BagSorter.Sort(bag, InventorySortOrder.Name, mask, Cols, Rows, groups);

            // Loose items take column 0 (4 rows) and the top of column 1
            for (int i = 0; i < 5; i++)
                Assert.AreSame(loose[i], bag.GetSlotItem(ColumnMajor(i)), "loose item " + i);
            // The synergy keeps its shape, anchored in column 2 (first column right of the sorted items)
            Assert.AreSame(s1, bag.GetSlotItem(0 * Cols + 2));
            Assert.AreSame(s2, bag.GetSlotItem(1 * Cols + 2));
            Assert.AreSame(s3, bag.GetSlotItem(1 * Cols + 3));
            Assert.AreEqual(8, bag.Count);
        }

        [TestMethod]
        public void Sort_RepeatedSorts_KeepSortedItemsOnTheLeft()
        {
            var bag = MakeBag();
            var sword = MakeGear("Sword", ItemKind.WeaponSword);
            var shield = MakeGear("Shield", ItemKind.Shield);
            bag.SetSlotItem(0, sword);
            bag.SetSlotItem(1, shield);
            var loose = MakeGear("Ring", ItemKind.Accessory);
            bag.SetSlotItem(50, loose);

            for (int pass = 0; pass < 3; pass++)
            {
                int swordIndex = -1, shieldIndex = -1;
                for (int i = 0; i < bag.Capacity; i++)
                {
                    if (bag.GetSlotItem(i) == sword) swordIndex = i;
                    if (bag.GetSlotItem(i) == shield) shieldIndex = i;
                }
                var mask = new bool[bag.Capacity];
                var groups = new List<IReadOnlyList<int>> { new List<int> { swordIndex, shieldIndex } };
                BagSorter.Sort(bag, (InventorySortOrder)(pass % 3), mask, Cols, Rows, groups);

                Assert.AreSame(loose, bag.GetSlotItem(0), "pass " + pass + ": the loose item stays leftmost");
                Assert.AreSame(sword, bag.GetSlotItem(1), "pass " + pass + ": synergy sits just right of it");
                Assert.AreSame(shield, bag.GetSlotItem(2));
            }
        }

        [TestMethod]
        public void Sort_GroupThatDoesNotFit_StaysWhereItWas()
        {
            // 2x2 bag: a vertical 2-cell group in column 0, a loose item and a stencil item in column 1.
            // After packing the loose item no 2-tall column is free, so the group must not be broken up.
            var bag = new ItemBag("Tiny", 4);
            var a = MakeGear("A", ItemKind.Shield);
            var b = MakeGear("B", ItemKind.Shield);
            var top = MakeGear("Top", ItemKind.Accessory);
            var bottom = MakeGear("Bottom", ItemKind.Accessory);
            bag.SetSlotItem(0, top);     // row 0 col 0
            bag.SetSlotItem(2, bottom);  // row 1 col 0
            bag.SetSlotItem(1, a);
            bag.SetSlotItem(3, b);

            var locked = new bool[4];
            locked[3] = true; // b is a stencil item, so column 1 never has two free cells
            var groups = new List<IReadOnlyList<int>> { new List<int> { 0, 2 } };
            BagSorter.Sort(bag, InventorySortOrder.Name, locked, 2, 2, groups);

            Assert.AreEqual(4, bag.Count, "no item is lost");
            Assert.AreSame(b, bag.GetSlotItem(3));
            Assert.AreSame(top, bag.GetSlotItem(0), "the synergy stays exactly where it was");
            Assert.AreSame(bottom, bag.GetSlotItem(2));
            Assert.AreSame(a, bag.GetSlotItem(1), "the loose item packs around it");
            Assert.AreEqual(bag.GetAcquireSequence(top) > 0, true, "pinned items keep their acquisition order");
        }
    }
}
