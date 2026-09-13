using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero.Services;
using PitHero.UI;
using RolePlayingFramework;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// The Party inventory grid went from 24 x 5 to 30 x 4 bag slots (shorter window). Saves store
    /// linear bag indices, so pre-v33 files are remapped on read to keep the player's arrangement.
    /// </summary>
    [TestClass]
    public class BagLayoutMigrationTests
    {
        [TestMethod]
        public void Constants_MatchTheLiveGrid()
        {
            Assert.AreEqual(InventoryGrid.BagColumns, BagLayoutMigration.CurrentColumns);
            Assert.AreEqual(InventoryGrid.BagRows, BagLayoutMigration.CurrentRows);
            Assert.AreEqual(InventoryGrid.BagCapacity, BagLayoutMigration.Capacity);
            Assert.AreEqual(BagLayoutMigration.Pre33Columns * BagLayoutMigration.Pre33Rows,
                BagLayoutMigration.CurrentColumns * BagLayoutMigration.CurrentRows,
                "both layouts must hold the same number of slots for a lossless remap");
        }

        [TestMethod]
        public void RemapPre33_IsABijectionOverTheBag()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < BagLayoutMigration.Capacity; i++)
            {
                int mapped = BagLayoutMigration.RemapPre33(i);
                Assert.IsTrue(mapped >= 0 && mapped < BagLayoutMigration.Capacity, $"index {i} mapped out of range to {mapped}");
                Assert.IsTrue(seen.Add(mapped), $"index {i} collides on {mapped}");
            }
            Assert.AreEqual(BagLayoutMigration.Capacity, seen.Count);
        }

        [TestMethod]
        public void RemapPre33_KeepsRowAndColumnForTheFirstFourRows()
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < BagLayoutMigration.Pre33Columns; col++)
                {
                    int old = row * BagLayoutMigration.Pre33Columns + col;
                    int mapped = BagLayoutMigration.RemapPre33(old);
                    Assert.AreEqual(row, mapped / BagLayoutMigration.CurrentColumns, $"old index {old} changed row");
                    Assert.AreEqual(col, mapped % BagLayoutMigration.CurrentColumns, $"old index {old} changed column");
                }
            }
        }

        [TestMethod]
        public void RemapPre33_SpillsTheOldFifthRowIntoTheNewColumns()
        {
            // Old row 4, columns 0..23 -> new rows 0..3, columns 24..29, six per row
            for (int col = 0; col < BagLayoutMigration.Pre33Columns; col++)
            {
                int old = 4 * BagLayoutMigration.Pre33Columns + col;
                int mapped = BagLayoutMigration.RemapPre33(old);
                Assert.AreEqual(col / 6, mapped / BagLayoutMigration.CurrentColumns, $"old index {old} landed on the wrong row");
                Assert.AreEqual(24 + col % 6, mapped % BagLayoutMigration.CurrentColumns, $"old index {old} landed on the wrong column");
            }

            Assert.AreEqual(24, BagLayoutMigration.RemapPre33(96));
            Assert.AreEqual(29, BagLayoutMigration.RemapPre33(101));
            Assert.AreEqual(30 + 24, BagLayoutMigration.RemapPre33(102));
            Assert.AreEqual(119, BagLayoutMigration.RemapPre33(119));
        }

        [TestMethod]
        public void RemapPre33_LeavesOutOfRangeIndicesAlone()
        {
            Assert.AreEqual(-1, BagLayoutMigration.RemapPre33(-1));
            Assert.AreEqual(120, BagLayoutMigration.RemapPre33(120));
            Assert.AreEqual(500, BagLayoutMigration.RemapPre33(500));
        }

        /// <summary>
        /// v33 writes the same bytes as v32; only the meaning of bag indices changed. A file stamped
        /// v32 must load with its item and shortcut indices remapped, and a v33 file must load as is.
        /// </summary>
        [TestMethod]
        public void SaveData_V32_File_RemapsBagIndices_V33_DoesNot()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                var original = new SaveData();
                original.HeroName = "Packrat";
                original.InventoryItems = new List<SavedItem>
                {
                    new SavedItem { Name = InventoryTextKey.Inv_HPPotion_Name, IsConsumable = true, StackCount = 5, SlotIndex = 0 },
                    new SavedItem { Name = InventoryTextKey.Inv_RustyBlade_Name, IsConsumable = false, StackCount = 0, SlotIndex = 25 },  // old (1,1)
                    new SavedItem { Name = InventoryTextKey.Inv_SquireHelm_Name, IsConsumable = false, StackCount = 0, SlotIndex = 100 }, // old (4,4)
                };
                original.ShortcutSlots = new List<SavedShortcutSlot>
                {
                    new SavedShortcutSlot { SlotType = 1, ItemBagIndex = 25, SkillId = null, OwnerMercIndex = -1 },
                };
                writer.Write(original);
            }

            byte[] v33 = ms.ToArray();

            var current = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v33)))
                rdr.ReadPersistableInto(current);
            Assert.AreEqual(25, current.InventoryItems[1].SlotIndex, "a current-version file is read verbatim");
            Assert.AreEqual(100, current.InventoryItems[2].SlotIndex);
            Assert.AreEqual(25, current.ShortcutSlots[0].ItemBagIndex);

            // Same bytes, stamped as the previous version
            var v32 = (byte[])v33.Clone();
            v32[0] = 32; v32[1] = 0; v32[2] = 0; v32[3] = 0;

            var legacy = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v32)))
                rdr.ReadPersistableInto(legacy);
            Assert.AreEqual(0, legacy.InventoryItems[0].SlotIndex, "row 0 column 0 stays put");
            Assert.AreEqual(1 * 30 + 1, legacy.InventoryItems[1].SlotIndex, "old (row 1, col 1) keeps its row and column");
            Assert.AreEqual(0 * 30 + 24 + 4, legacy.InventoryItems[2].SlotIndex, "old row 4 col 4 spills into new column 28 of row 0");
            Assert.AreEqual(1 * 30 + 1, legacy.ShortcutSlots[0].ItemBagIndex, "shortcuts follow their items");
            Assert.AreEqual("Packrat", legacy.HeroName);
        }
    }
}
