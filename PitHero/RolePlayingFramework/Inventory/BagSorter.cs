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
    /// Rigid groups (unbound synergies) are lifted out, then set back down intact to the right of the
    /// sorted items, so they keep their synergy without blocking the left-hand packing. A group whose
    /// shape fits nowhere is left exactly where it was rather than broken up.
    /// Runs inside a replayed command, so ordering is a total order with no culture-dependent compares.
    /// </summary>
    public static class BagSorter
    {
        /// <summary>
        /// Sorts <paramref name="bag"/> in place. <paramref name="lockedMask"/> may be null (nothing locked).
        /// <paramref name="rigidGroups"/> are disjoint sets of bag indices whose items move together, keeping
        /// their relative layout; a group touching a locked or out-of-range slot stays where it is.
        /// </summary>
        public static void Sort(ItemBag bag, InventorySortOrder order, bool[] lockedMask, int columns, int rows,
            IReadOnlyList<IReadOnlyList<int>> rigidGroups = null)
        {
            if (bag == null || columns <= 0 || rows <= 0) return;
            int cells = columns * rows;
            if (cells > bag.Capacity) cells = bag.Capacity;

            var locked = new bool[cells];
            for (int i = 0; i < cells; i++) locked[i] = IsLocked(lockedMask, i);

            var groups = CollectGroups(bag, rigidGroups, locked, cells, columns);
            var inGroup = new bool[cells];
            for (int g = 0; g < groups.Count; g++)
                for (int c = 0; c < groups[g].Indices.Count; c++) inGroup[groups[g].Indices[c]] = true;

            var items = new List<IItem>(bag.Count);
            for (int i = 0; i < cells; i++)
            {
                if (locked[i] || inGroup[i]) continue;
                var item = bag.GetSlotItem(i);
                if (item != null) items.Add(item);
            }
            if (items.Count == 0 && groups.Count == 0) return;

            // Snapshot acquisition order first: clearing slots below forgets it
            var seqs = new Dictionary<IItem, long>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < items.Count; i++) seqs[items[i]] = bag.GetAcquireSequence(items[i]);
            for (int g = 0; g < groups.Count; g++)
                for (int c = 0; c < groups[g].Items.Count; c++) seqs[groups[g].Items[c]] = bag.GetAcquireSequence(groups[g].Items[c]);

            // Every comparer ends on acquisition order, which is unique per instance, so the order is total
            // and List.Sort's instability cannot matter
            Comparison<IItem> compare = order switch
            {
                InventorySortOrder.Type => (a, b) => CompareType(a, b, seqs),
                InventorySortOrder.Name => (a, b) => CompareName(a, b, seqs),
                _ => (a, b) => CompareTime(a, b, seqs),
            };
            items.Sort(compare);

            // Plan the layout on a scratch grid. When a group's shape fits nowhere, pin it where it already is
            // and plan again; each retry pins one more group, so this ends. Sorted items always fit, because
            // every one of them (and every lifted group item) came from an unlocked slot.
            var plan = new IItem[cells];
            while (!TryPlan(bag, items, groups, locked, plan, cells, columns, rows, out int failedGroup))
            {
                var pinned = groups[failedGroup];
                for (int c = 0; c < pinned.Indices.Count; c++) locked[pinned.Indices[c]] = true;
                groups.RemoveAt(failedGroup);
            }

            for (int i = 0; i < cells; i++)
                if (!locked[i]) bag.SetSlotItem(i, null);
            for (int i = 0; i < cells; i++)
            {
                if (locked[i] || plan[i] == null) continue;
                bag.SetSlotItem(i, plan[i]);
                bag.SetAcquireSequence(plan[i], seqs[plan[i]]);
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

        /// <summary>A rigid group lifted out of the bag: its occupied slots (row-major indices) and their items, index-aligned.</summary>
        private sealed class RigidGroup
        {
            public readonly List<int> Indices = new List<int>();
            public readonly List<IItem> Items = new List<IItem>();
            public int OrderKey;   // smallest column-major position, so groups go back down in their original left-to-right order
        }

        /// <summary>
        /// Fills <paramref name="plan"/> (non-locked cells only): sorted items packed column-major, then each group first-fit
        /// right of the last sorted column, else anywhere. Returns false with the index of the first group that fits nowhere.
        /// </summary>
        private static bool TryPlan(ItemBag bag, List<IItem> items, List<RigidGroup> groups, bool[] locked, IItem[] plan,
            int cells, int columns, int rows, out int failedGroup)
        {
            failedGroup = -1;
            Array.Clear(plan, 0, plan.Length);

            int next = 0;
            int lastColumn = -1;
            for (int col = 0; col < columns && next < items.Count; col++)
            {
                for (int row = 0; row < rows && next < items.Count; row++)
                {
                    int index = row * columns + col;
                    if (index >= cells || locked[index]) continue;
                    plan[index] = items[next++];
                    lastColumn = col;
                }
            }

            int startColumn = lastColumn + 1;
            for (int g = 0; g < groups.Count; g++)
            {
                if (TryPlaceGroup(groups[g], locked, plan, cells, columns, rows, startColumn)) continue;
                if (TryPlaceGroup(groups[g], locked, plan, cells, columns, rows, 0)) continue;
                failedGroup = g;
                return false;
            }
            return true;
        }

        private static List<RigidGroup> CollectGroups(ItemBag bag, IReadOnlyList<IReadOnlyList<int>> rigidGroups,
            bool[] locked, int cells, int columns)
        {
            var result = new List<RigidGroup>();
            if (rigidGroups == null) return result;

            int rows = cells / columns;
            var claimed = new bool[cells];
            var unmovable = new List<IReadOnlyList<int>>();
            for (int g = 0; g < rigidGroups.Count; g++)
            {
                var source = rigidGroups[g];
                if (source == null || source.Count == 0) continue;

                bool movable = true;
                for (int i = 0; i < source.Count && movable; i++)
                {
                    int index = source[i];
                    if (index < 0 || index >= cells || locked[index] || claimed[index]) movable = false;
                }
                if (!movable)
                {
                    unmovable.Add(source); // stays exactly where it is (locked below)
                    continue;
                }

                var group = new RigidGroup { OrderKey = int.MaxValue };
                for (int i = 0; i < source.Count; i++)
                {
                    int index = source[i];
                    claimed[index] = true;
                    var item = bag.GetSlotItem(index);
                    if (item == null) continue;
                    group.Indices.Add(index);
                    group.Items.Add(item);
                    int key = (index % columns) * rows + index / columns;
                    if (key < group.OrderKey) group.OrderKey = key;
                }
                if (group.Items.Count > 0) result.Add(group);
            }

            // Unmovable groups must not be shuffled by the sort either
            for (int g = 0; g < unmovable.Count; g++)
            {
                var source = unmovable[g];
                for (int i = 0; i < source.Count; i++)
                    if (source[i] >= 0 && source[i] < cells) locked[source[i]] = true;
            }

            result.Sort((a, b) => a.OrderKey.CompareTo(b.OrderKey));
            return result;
        }

        /// <summary>First-fit placement of a group's shape into the plan, anchors scanned column-major from <paramref name="startColumn"/>.</summary>
        private static bool TryPlaceGroup(RigidGroup group, bool[] locked, IItem[] plan, int cells, int columns, int rows, int startColumn)
        {
            int minRow = int.MaxValue, minCol = int.MaxValue, maxRow = 0, maxCol = 0;
            for (int i = 0; i < group.Indices.Count; i++)
            {
                int r = group.Indices[i] / columns, c = group.Indices[i] % columns;
                if (r < minRow) minRow = r;
                if (c < minCol) minCol = c;
                if (r > maxRow) maxRow = r;
                if (c > maxCol) maxCol = c;
            }
            int height = maxRow - minRow + 1, width = maxCol - minCol + 1;

            for (int col = Math.Max(0, startColumn); col + width <= columns; col++)
            {
                for (int row = 0; row + height <= rows; row++)
                {
                    bool fits = true;
                    for (int i = 0; i < group.Indices.Count && fits; i++)
                    {
                        int target = TargetIndex(group.Indices[i], minRow, minCol, row, col, columns);
                        fits = target < cells && !locked[target] && plan[target] == null;
                    }
                    if (!fits) continue;

                    for (int i = 0; i < group.Indices.Count; i++)
                        plan[TargetIndex(group.Indices[i], minRow, minCol, row, col, columns)] = group.Items[i];
                    return true;
                }
            }
            return false;
        }

        private static int TargetIndex(int sourceIndex, int minRow, int minCol, int anchorRow, int anchorCol, int columns)
        {
            int r = sourceIndex / columns - minRow + anchorRow;
            int c = sourceIndex % columns - minCol + anchorCol;
            return r * columns + c;
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
