using System.Collections.Generic;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>One harvested-crop stack slot. Count 0 means the slot is empty.</summary>
    public struct HarvestSlot
    {
        public CropType Type;
        public int Count;
        public bool IsEmpty => Count <= 0;
    }

    /// <summary>
    /// Per-Crop-Storage-building inventory of harvested crops. Each building owns
    /// <see cref="SlotsPerBuilding"/> slots; each slot holds a stack of a single crop type up to
    /// that crop's max stack size (<see cref="CropConfig.GetMaxHarvestStack"/>).
    /// </summary>
    public class CropStorageInventoryService
    {
        /// <summary>Slots available per Crop Storage building (8×4 grid).</summary>
        public const int SlotsPerBuilding = 32;

        private readonly Dictionary<int, HarvestSlot[]> _byBuilding = new Dictionary<int, HarvestSlot[]>();
        private readonly BuildingService _buildingService;

        public CropStorageInventoryService(BuildingService buildingService)
        {
            _buildingService = buildingService;
            if (_buildingService != null)
                _buildingService.BuildingsChanged += PruneRemovedBuildings;
        }

        private HarvestSlot[] GetOrCreate(int buildingId)
        {
            if (!_byBuilding.TryGetValue(buildingId, out var slots))
            {
                slots = new HarvestSlot[SlotsPerBuilding];
                _byBuilding[buildingId] = slots;
            }
            return slots;
        }

        /// <summary>
        /// True if the building can accept one more of this crop — either an existing non-full
        /// stack of that crop, or a free slot.
        /// </summary>
        public bool HasCapacityFor(int buildingId, CropType crop)
        {
            var slots = GetOrCreate(buildingId);
            int max = CropConfig.GetMaxHarvestStack(crop);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                    return true;
                if (slots[i].Type == crop && slots[i].Count < max)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Adds <paramref name="amount"/> harvested crops to the building, spilling across existing
        /// non-full stacks and empty slots as needed. Returns true if at least one unit was stored.
        /// Any remainder that doesn't fit (all slots full) is dropped.
        /// </summary>
        public bool TryDeposit(int buildingId, CropType crop, int amount = 1)
            => DepositReturningStored(buildingId, crop, amount) > 0;

        /// <summary>
        /// Adds up to <paramref name="amount"/> harvested crops to the building, spilling across
        /// existing non-full stacks (topped off first) then empty slots. Returns the number of units
        /// actually stored (0..amount); any remainder that doesn't fit is not stored.
        /// </summary>
        public int DepositReturningStored(int buildingId, CropType crop, int amount)
        {
            if (amount <= 0)
                return 0;

            var slots = GetOrCreate(buildingId);
            int max = CropConfig.GetMaxHarvestStack(crop);
            int remaining = amount;

            // Top off existing non-full stacks of this crop first
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Type == crop && slots[i].Count < max)
                {
                    int room = max - slots[i].Count;
                    int add = room < remaining ? room : remaining;
                    slots[i].Count += add;
                    remaining -= add;
                }
            }

            // Then spill into empty slots
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int add = max < remaining ? max : remaining;
                    slots[i].Type = crop;
                    slots[i].Count = add;
                    remaining -= add;
                }
            }

            return amount - remaining;
        }

        /// <summary>Returns the slot array for a building (creating an empty one if needed). Read-only view for UI.</summary>
        public IReadOnlyList<HarvestSlot> GetSlots(int buildingId) => GetOrCreate(buildingId);

        /// <summary>True if every slot in the building is empty (no harvested crops stored).</summary>
        public bool IsEmpty(int buildingId)
        {
            var slots = GetOrCreate(buildingId);
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].IsEmpty)
                    return false;
            return true;
        }

        /// <summary>Empties a single slot (used when a stack is sold).</summary>
        public void ClearSlot(int buildingId, int slotIndex)
        {
            var slots = GetOrCreate(buildingId);
            if (slotIndex >= 0 && slotIndex < slots.Length)
                slots[slotIndex] = default;
        }

        /// <summary>Empties every slot in the building (used when all crops are sold).</summary>
        public void ClearBuilding(int buildingId)
        {
            var slots = GetOrCreate(buildingId);
            for (int i = 0; i < slots.Length; i++)
                slots[i] = default;
        }

        /// <summary>
        /// Moves all crops out of the source building and redistributes them across the other Crop
        /// Storage buildings, merging into existing stacks and spilling into empty slots, filling each
        /// destination before moving to the next. Any crops that don't fit anywhere are left in the
        /// source building. Returns the number of units moved out.
        /// </summary>
        public int MoveAllCropsToOtherStorages(int sourceId)
        {
            if (_buildingService == null)
                return 0;

            var sourceSlots = GetOrCreate(sourceId);
            var all = _buildingService.GetAll();
            int totalMoved = 0;

            for (int s = 0; s < sourceSlots.Length; s++)
            {
                if (sourceSlots[s].IsEmpty)
                    continue;

                var crop = sourceSlots[s].Type;
                int remaining = sourceSlots[s].Count;

                for (int b = 0; b < all.Count && remaining > 0; b++)
                {
                    var dest = all[b];
                    if (dest.Type != BuildingType.CropStorage || dest.UniqueId == sourceId)
                        continue;
                    int stored = DepositReturningStored(dest.UniqueId, crop, remaining);
                    remaining -= stored;
                    totalMoved += stored;
                }

                // Write back leftover (clears the slot when fully moved).
                if (remaining <= 0)
                    sourceSlots[s] = default;
                else
                    sourceSlots[s].Count = remaining;
            }

            return totalMoved;
        }

        // ── Save / restore ────────────────────────────────────────────────────────

        /// <summary>Enumerates all building inventories for serialization.</summary>
        public IEnumerable<KeyValuePair<int, HarvestSlot[]>> GetAllInventories() => _byBuilding;

        /// <summary>Replaces a building's inventory from saved data.</summary>
        public void RestoreInventory(int buildingId, HarvestSlot[] slots)
        {
            var dst = GetOrCreate(buildingId);
            int copy = slots.Length < SlotsPerBuilding ? slots.Length : SlotsPerBuilding;
            for (int i = 0; i < copy; i++)
                dst[i] = slots[i];
            for (int i = copy; i < SlotsPerBuilding; i++)
                dst[i] = default;
        }

        /// <summary>Clears all inventories (called when loading a save or quitting to title).</summary>
        public void Clear() => _byBuilding.Clear();

        private void PruneRemovedBuildings()
        {
            if (_buildingService == null)
                return;

            var all = _buildingService.GetAll();
            // Collect ids that still exist as Crop Storage buildings
            _liveIds.Clear();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == BuildingType.CropStorage)
                    _liveIds.Add(all[i].UniqueId);

            _removeScratch.Clear();
            foreach (var kvp in _byBuilding)
                if (!_liveIds.Contains(kvp.Key))
                    _removeScratch.Add(kvp.Key);

            for (int i = 0; i < _removeScratch.Count; i++)
                _byBuilding.Remove(_removeScratch[i]);
        }

        private readonly HashSet<int> _liveIds = new HashSet<int>();
        private readonly List<int> _removeScratch = new List<int>();
    }
}
