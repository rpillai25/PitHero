using PitHero.UI;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>
    /// Works out which hero bag slots sorting must respect. Placed stencil cells are locked (mask index = bag index).
    /// Active synergies not bound to a stencil become rigid groups the sorter relocates intact; a synergy it cannot
    /// move (touching an equipment slot or a stencil cell) is locked instead.
    /// </summary>
    public static class BagSortLockMask
    {
        /// <summary>Locks every bag slot covered by a cell of a placed stencil.</summary>
        public static void AddPlacedStencils(IReadOnlyList<PlacedStencilRecord> placedStencils, bool[] mask)
        {
            if (mask == null || placedStencils == null) return;

            for (int s = 0; s < placedStencils.Count; s++)
            {
                var record = placedStencils[s];
                var pattern = SynergyPatternRegistry.GetById(record.PatternId);
                if (pattern == null) continue;

                var offsets = pattern.GridOffsets;
                for (int i = 0; i < offsets.Count; i++)
                {
                    int bagIndex = ToBagIndex(record.AnchorX + offsets[i].X, record.AnchorY + offsets[i].Y);
                    if (bagIndex >= 0 && bagIndex < mask.Length) mask[bagIndex] = true;
                }
            }
        }

        /// <summary>
        /// Call after <see cref="AddPlacedStencils"/>. Overlapping synergies merge into one group. A group whose every
        /// cell is an unlocked bag slot is returned as a movable rigid group (bag indices); any other group's bag slots
        /// are locked in <paramref name="mask"/>.
        /// </summary>
        public static List<IReadOnlyList<int>> AddActiveSynergies(IReadOnlyList<ActiveSynergy> activeSynergies, bool[] mask)
        {
            var movable = new List<IReadOnlyList<int>>();
            if (mask == null || activeSynergies == null || activeSynergies.Count == 0) return movable;

            int count = activeSynergies.Count;
            var parent = new int[count];
            var instanceMovable = new bool[count];
            var cellsOf = new List<int>[count];
            var ownerOfSlot = new int[mask.Length];
            for (int i = 0; i < ownerOfSlot.Length; i++) ownerOfSlot[i] = -1;

            for (int s = 0; s < count; s++)
            {
                parent[s] = s;
                cellsOf[s] = new List<int>();
                instanceMovable[s] = true;
                var slots = activeSynergies[s]?.AffectedSlots;
                if (slots == null) { instanceMovable[s] = false; continue; }

                for (int i = 0; i < slots.Count; i++)
                {
                    int bagIndex = ToBagIndex(slots[i].X, slots[i].Y);
                    if (bagIndex < 0 || bagIndex >= mask.Length) { instanceMovable[s] = false; continue; }
                    if (mask[bagIndex]) instanceMovable[s] = false;   // bound to a stencil cell
                    cellsOf[s].Add(bagIndex);

                    // Synergies sharing a slot must move (or stay) together
                    if (ownerOfSlot[bagIndex] >= 0) Union(parent, ownerOfSlot[bagIndex], s);
                    else ownerOfSlot[bagIndex] = s;
                }
            }

            // Resolve groups in first-instance order (deterministic)
            var groupRoots = new List<int>();
            var groupCells = new List<List<int>>();
            var groupMovable = new List<bool>();
            for (int s = 0; s < count; s++)
            {
                int root = Find(parent, s);
                int g = groupRoots.IndexOf(root);
                if (g < 0)
                {
                    g = groupRoots.Count;
                    groupRoots.Add(root);
                    groupCells.Add(new List<int>());
                    groupMovable.Add(true);
                }
                if (!instanceMovable[s]) groupMovable[g] = false;
                for (int i = 0; i < cellsOf[s].Count; i++)
                    if (!groupCells[g].Contains(cellsOf[s][i])) groupCells[g].Add(cellsOf[s][i]);
            }

            for (int g = 0; g < groupRoots.Count; g++)
            {
                if (groupMovable[g])
                {
                    groupCells[g].Sort();
                    movable.Add(groupCells[g]);
                }
                else
                {
                    for (int i = 0; i < groupCells[g].Count; i++) mask[groupCells[g][i]] = true;
                }
            }
            return movable;
        }

        /// <summary>Bag index for full-grid coordinates, or -1 off the bag rows: (gridY - BagRowStart) * BagColumns + gridX.</summary>
        private static int ToBagIndex(int gridX, int gridY)
        {
            int lastBagRow = InventoryGrid.BagRowStart + InventoryGrid.BagRows - 1;
            if (gridY < InventoryGrid.BagRowStart || gridY > lastBagRow) return -1;
            if (gridX < 0 || gridX >= InventoryGrid.BagColumns) return -1;
            return (gridY - InventoryGrid.BagRowStart) * InventoryGrid.BagColumns + gridX;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra == rb) return;
            if (ra < rb) parent[rb] = ra; else parent[ra] = rb;
        }
    }
}
