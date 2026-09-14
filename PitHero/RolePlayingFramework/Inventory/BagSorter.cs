using PitHero.UI;
using RolePlayingFramework.Equipment;
using System;
using System.Collections.Generic;

namespace RolePlayingFramework.Inventory
{
    /// <summary>
    /// Sorts a bag laid out as a row-major grid (index = row * columns + col). Sorted items are packed
    /// column by column from the leftmost column, top to bottom, so free space collects on the right
    /// where the player places stencils. Locked slots (stencil cells) are neither read nor filled.
    /// Runs inside a replayed command, so ordering is a total order with no culture-dependent compares.
    /// </summary>
    public static class BagSorter
    {
        /// <summary>Sorts <paramref name="bag"/> in place. <paramref name="lockedMask"/> may be null (nothing locked).</summary>
        public static void Sort(ItemBag bag, InventorySortOrder order, bool[] lockedMask, int columns, int rows)
        {
            if (bag == null || columns <= 0 || rows <= 0) return;
            int cells = columns * rows;
            if (cells > bag.Capacity) cells = bag.Capacity;

            var items = new List<IItem>(bag.Count);
            for (int i = 0; i < cells; i++)
            {
                if (IsLocked(lockedMask, i)) continue;
                var item = bag.GetSlotItem(i);
                if (item != null) items.Add(item);
            }
            if (items.Count == 0) return;

            // Snapshot acquisition order first: clearing slots below forgets it
            var seqs = new Dictionary<IItem, long>(items.Count, ReferenceEqualityComparer.Instance);
            for (int i = 0; i < items.Count; i++) seqs[items[i]] = bag.GetAcquireSequence(items[i]);

            // Every comparer ends on acquisition order, which is unique per instance, so the order is total
            // and List.Sort's instability cannot matter
            Comparison<IItem> compare = order switch
            {
                InventorySortOrder.Type => (a, b) => CompareType(a, b, seqs),
                InventorySortOrder.Name => (a, b) => CompareName(a, b, seqs),
                _ => (a, b) => CompareTime(a, b, seqs),
            };
            items.Sort(compare);

            for (int i = 0; i < cells; i++)
                if (!IsLocked(lockedMask, i)) bag.SetSlotItem(i, null);

            int next = 0;
            for (int col = 0; col < columns && next < items.Count; col++)
            {
                for (int row = 0; row < rows && next < items.Count; row++)
                {
                    int index = row * columns + col;
                    if (index >= cells || IsLocked(lockedMask, index)) continue;
                    var item = items[next++];
                    bag.SetSlotItem(index, item);
                    bag.SetAcquireSequence(item, seqs[item]);
                }
            }
        }

        /// <summary>Category rank for Sort by Type: weapons, armor, hats, shields, accessories, consumables last.</summary>
        public static int TypeRank(ItemKind kind)
        {
            return kind switch
            {
                ItemKind.Consumable => 100,
                // Gear kinds are declared weapons → armor → hats → shield → accessory
                _ => (int)kind,
            };
        }

        private static bool IsLocked(bool[] mask, int index) => mask != null && index < mask.Length && mask[index];

        private static int CompareTime(IItem a, IItem b, Dictionary<IItem, long> seqs)
        {
            int c = seqs[b].CompareTo(seqs[a]); // newest first
            if (c != 0) return c;
            return string.CompareOrdinal(a.Name, b.Name);
        }

        private static int CompareType(IItem a, IItem b, Dictionary<IItem, long> seqs)
        {
            int c = TypeRank(a.Kind).CompareTo(TypeRank(b.Kind));
            if (c != 0) return c;
            return CompareName(a, b, seqs);
        }

        private static int CompareName(IItem a, IItem b, Dictionary<IItem, long> seqs)
        {
            int c = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Name, b.Name);
            if (c != 0) return c;
            return seqs[b].CompareTo(seqs[a]);
        }
    }
}
