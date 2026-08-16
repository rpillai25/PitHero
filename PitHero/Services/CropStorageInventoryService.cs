using System.Collections.Generic;
using Nez;
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

        // Held-for-transfer ledger (issue #386): units a carrying kitchen runner has picked up
        // but not yet unloaded at the fridge. The units stay physically in their slots — so a
        // save or quit at any moment loses nothing — while every withdraw, count, and display
        // path treats them as absent. Keyed building → per-crop reserved units. Transient:
        // never saved; runners re-fetch after a load.
        private readonly Dictionary<int, int[]> _reservedByBuilding = new Dictionary<int, int[]>();

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
            ClampReservations(buildingId);
        }

        /// <summary>Empties every slot in the building (used when all crops are sold).</summary>
        public void ClearBuilding(int buildingId)
        {
            var slots = GetOrCreate(buildingId);
            for (int i = 0; i < slots.Length; i++)
                slots[i] = default;
            ClampReservations(buildingId);
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

            // Moved-out units may have been held for transfer — clamp so the holder shorts
            // gracefully instead of consuming crops that now live elsewhere.
            ClampReservations(sourceId);
            return totalMoved;
        }

        // ── Save / restore ────────────────────────────────────────────────────────

        /// <summary>Enumerates all building inventories for serialization.</summary>
        public IEnumerable<KeyValuePair<int, HarvestSlot[]>> GetAllInventories() => _byBuilding;

        /// <summary>Copies all building ids that have an inventory into <paramref name="dest"/> (cleared first).</summary>
        public void CopyBuildingIds(List<int> dest)
        {
            dest.Clear();
            foreach (var kvp in _byBuilding)
                dest.Add(kvp.Key);
        }

        /// <summary>Replaces a building's inventory from saved data. Any held-for-transfer state is discarded.</summary>
        public void RestoreInventory(int buildingId, HarvestSlot[] slots)
        {
            var dst = GetOrCreate(buildingId);
            int copy = slots.Length < SlotsPerBuilding ? slots.Length : SlotsPerBuilding;
            for (int i = 0; i < copy; i++)
                dst[i] = slots[i];
            for (int i = copy; i < SlotsPerBuilding; i++)
                dst[i] = default;
            _reservedByBuilding.Remove(buildingId);
        }

        /// <summary>Clears all inventories and holds (called when loading a save or quitting to title).</summary>
        public void Clear()
        {
            _byBuilding.Clear();
            _reservedByBuilding.Clear();
        }

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
            {
                _byBuilding.Remove(_removeScratch[i]);
                _reservedByBuilding.Remove(_removeScratch[i]);
            }
        }

        private readonly HashSet<int> _liveIds = new HashSet<int>();
        private readonly List<int> _removeScratch = new List<int>();

        // ── Kitchen withdraw / deposit helpers ────────────────────────────────────

        /// <summary>Total units of <paramref name="crop"/> stored across all Crop Storage buildings.</summary>
        public int CountTotal(Farming.CropType crop)
        {
            if (_buildingService == null)
                return 0;

            int total = 0;
            var all = _buildingService.GetAll();
            for (int b = 0; b < all.Count; b++)
            {
                if (all[b].Type != BuildingType.CropStorage)
                    continue;
                var slots = GetOrCreate(all[b].UniqueId);
                for (int s = 0; s < slots.Length; s++)
                {
                    if (!slots[s].IsEmpty && slots[s].Type == crop)
                        total += slots[s].Count;
                }
            }
            return total;
        }

        /// <summary>Units of <paramref name="crop"/> stored in one specific Crop Storage building.</summary>
        public int CountIn(int buildingId, Farming.CropType crop)
        {
            var slots = GetOrCreate(buildingId);
            int total = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                if (!slots[s].IsEmpty && slots[s].Type == crop)
                    total += slots[s].Count;
            }
            return total;
        }

        /// <summary>
        /// Best-effort withdrawal of up to <paramref name="max"/> units of <paramref name="crop"/>
        /// from one specific building, never touching units held for transfer by a carrying
        /// runner. Returns how many were actually taken (0 if none are available).
        /// </summary>
        public int WithdrawUpTo(int buildingId, Farming.CropType crop, int max)
        {
            int available = AvailableIn(buildingId, crop);
            if (max > available) max = available;
            return RemovePhysicalUpTo(buildingId, crop, max);
        }

        /// <summary>Removes up to max physical units of the crop from the building's slots, in slot order.</summary>
        private int RemovePhysicalUpTo(int buildingId, Farming.CropType crop, int max)
        {
            if (max <= 0)
                return 0;

            var slots = GetOrCreate(buildingId);
            int remaining = max;
            for (int s = 0; s < slots.Length && remaining > 0; s++)
            {
                if (slots[s].IsEmpty || slots[s].Type != crop)
                    continue;
                int take = slots[s].Count < remaining ? slots[s].Count : remaining;
                slots[s].Count -= take;
                if (slots[s].Count <= 0)
                    slots[s] = default;
                remaining -= take;
            }
            return max - remaining;
        }

        /// <summary>
        /// All-or-nothing withdrawal of <paramref name="amount"/> units of <paramref name="crop"/>
        /// across all Crop Storage buildings, drawing only on AVAILABLE units (held-for-transfer
        /// units are invisible here). Returns false without mutating if availability is insufficient.
        /// When <paramref name="sourceBuildingIds"/> is given, each building actually drawn from is
        /// appended to it (no duplicates) so callers can retrace where the crops came from.
        /// </summary>
        public bool TryWithdrawAcrossBuildings(Farming.CropType crop, int amount,
            List<int> sourceBuildingIds = null)
        {
            if (amount <= 0)
                return true;
            if (AvailableTotal(crop) < amount)
                return false;

            if (_buildingService == null)
                return false;

            int remaining = amount;
            var all = _buildingService.GetAll();
            for (int b = 0; b < all.Count && remaining > 0; b++)
            {
                if (all[b].Type != BuildingType.CropStorage)
                    continue;
                int took = WithdrawUpTo(all[b].UniqueId, crop, remaining);
                if (took <= 0)
                    continue;
                remaining -= took;
                if (sourceBuildingIds != null && !sourceBuildingIds.Contains(all[b].UniqueId))
                    sourceBuildingIds.Add(all[b].UniqueId);
            }
            return true;
        }

        /// <summary>
        /// Refund path: deposits <paramref name="amount"/> units of <paramref name="crop"/> back into
        /// storage across all buildings. Any units that don't fit are dropped near the first storage's
        /// door tile via DroppedCropService — crops are never silently destroyed.
        /// </summary>
        public void DepositAcrossBuildings(Farming.CropType crop, int amount)
        {
            if (amount <= 0 || _buildingService == null)
                return;

            int remaining = amount;
            var all = _buildingService.GetAll();
            for (int b = 0; b < all.Count && remaining > 0; b++)
            {
                if (all[b].Type != BuildingType.CropStorage)
                    continue;
                int stored = DepositReturningStored(all[b].UniqueId, crop, remaining);
                remaining -= stored;
            }

            if (remaining > 0)
            {
                // All storages full — drop near first storage door so workers can recover them
                for (int b = 0; b < all.Count; b++)
                {
                    if (all[b].Type != BuildingType.CropStorage)
                        continue;
                    var door = Util.BuildingConfig.GetDoorTile(all[b].Type,
                        new Microsoft.Xna.Framework.Point(all[b].TileX, all[b].TileY));
                    Core.Services.GetService<DroppedCropService>()?.Drop(crop, remaining, door);
                    break;
                }
            }
        }

        // ── Held-for-transfer reservations (issue #386) ──────────────────────────

        private int[] GetOrCreateReserved(int buildingId)
        {
            if (!_reservedByBuilding.TryGetValue(buildingId, out var reserved))
            {
                reserved = new int[Farming.CropTypeInfo.Count];
                _reservedByBuilding[buildingId] = reserved;
            }
            return reserved;
        }

        /// <summary>Units of the crop in this building currently held for transfer by carrying runners.</summary>
        public int ReservedIn(int buildingId, Farming.CropType crop)
            => _reservedByBuilding.TryGetValue(buildingId, out var reserved) ? reserved[(int)crop] : 0;

        /// <summary>Units of the crop in this building actually available (physical minus held-for-transfer).</summary>
        public int AvailableIn(int buildingId, Farming.CropType crop)
        {
            int available = CountIn(buildingId, crop) - ReservedIn(buildingId, crop);
            return available > 0 ? available : 0;
        }

        /// <summary>Units of the crop available across all Crop Storage buildings (physical minus held-for-transfer).</summary>
        public int AvailableTotal(Farming.CropType crop)
        {
            if (_buildingService == null)
                return 0;

            int total = 0;
            var all = _buildingService.GetAll();
            for (int b = 0; b < all.Count; b++)
            {
                if (all[b].Type == BuildingType.CropStorage)
                    total += AvailableIn(all[b].UniqueId, crop);
            }
            return total;
        }

        /// <summary>
        /// Holds up to <paramref name="qty"/> available units of the crop for transfer: the units
        /// stay physically in their slots but disappear from every count, withdraw, and display
        /// path until <see cref="WithdrawReserved"/> consumes or <see cref="ReleaseReserved"/>
        /// frees them. Returns the units actually granted.
        /// </summary>
        public int Reserve(int buildingId, Farming.CropType crop, int qty)
        {
            if (qty <= 0)
                return 0;
            int available = AvailableIn(buildingId, crop);
            int granted = qty < available ? qty : available;
            if (granted > 0)
                GetOrCreateReserved(buildingId)[(int)crop] += granted;
            return granted;
        }

        /// <summary>Frees held-for-transfer units back to availability (trip abandoned — nothing ever moved).</summary>
        public void ReleaseReserved(int buildingId, Farming.CropType crop, int qty)
        {
            if (qty <= 0 || !_reservedByBuilding.TryGetValue(buildingId, out var reserved))
                return;
            int next = reserved[(int)crop] - qty;
            reserved[(int)crop] = next > 0 ? next : 0;
        }

        /// <summary>
        /// Consumes held-for-transfer units at unload time: physically removes up to
        /// <paramref name="qty"/> of this caller's hold from the building. Shorts gracefully when
        /// the physical units shrank since the hold was taken (building sold, crops moved) —
        /// never touches another runner's hold. Returns the units removed.
        /// </summary>
        public int WithdrawReserved(int buildingId, Farming.CropType crop, int qty)
        {
            if (qty <= 0 || !_reservedByBuilding.TryGetValue(buildingId, out var reserved))
                return 0;

            int myShare = reserved[(int)crop] < qty ? reserved[(int)crop] : qty;
            reserved[(int)crop] -= myShare;

            // Available now includes the share just released but still excludes other holds
            int cap = AvailableIn(buildingId, crop);
            int take = myShare < cap ? myShare : cap;
            return RemovePhysicalUpTo(buildingId, crop, take);
        }

        /// <summary>
        /// Re-clamps every hold in the building to what is physically present. Called after any
        /// mutation that removes units outside the reservation system (sell, move-all, restore)
        /// so a hold can never exceed reality; the holder's unload then shorts gracefully.
        /// </summary>
        private void ClampReservations(int buildingId)
        {
            if (!_reservedByBuilding.TryGetValue(buildingId, out var reserved))
                return;
            for (int c = 0; c < reserved.Length; c++)
            {
                if (reserved[c] <= 0)
                    continue;
                int physical = CountIn(buildingId, (Farming.CropType)c);
                if (reserved[c] > physical)
                    reserved[c] = physical;
            }
        }

        // ── Display view (held units hidden) ─────────────────────────────────────

        /// <summary>
        /// Copies the building's slots into <paramref name="buffer"/> with held-for-transfer
        /// units subtracted (in slot order, mirroring the order withdrawals drain), so the UI
        /// shows only what is actually available.
        /// </summary>
        public void CopyDisplaySlots(int buildingId, HarvestSlot[] buffer)
        {
            var slots = GetOrCreate(buildingId);
            int copy = slots.Length < buffer.Length ? slots.Length : buffer.Length;
            for (int i = 0; i < copy; i++)
                buffer[i] = slots[i];
            for (int i = copy; i < buffer.Length; i++)
                buffer[i] = default;

            if (!_reservedByBuilding.TryGetValue(buildingId, out var reserved))
                return;
            for (int c = 0; c < reserved.Length; c++)
            {
                int hide = reserved[c];
                if (hide <= 0)
                    continue;
                var crop = (Farming.CropType)c;
                for (int s = 0; s < copy && hide > 0; s++)
                {
                    if (buffer[s].IsEmpty || buffer[s].Type != crop)
                        continue;
                    int sub = buffer[s].Count < hide ? buffer[s].Count : hide;
                    buffer[s].Count -= sub;
                    if (buffer[s].Count <= 0)
                        buffer[s] = default;
                    hide -= sub;
                }
            }
        }

        /// <summary>True if the building has at least one available (non-held) crop unit to show or sell.</summary>
        public bool HasAvailableCrops(int buildingId)
        {
            var slots = GetOrCreate(buildingId);
            for (int s = 0; s < slots.Length; s++)
            {
                if (slots[s].IsEmpty)
                    continue;
                if (AvailableIn(buildingId, slots[s].Type) > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Sell support: removes up to <paramref name="maxUnits"/> units from one specific slot,
        /// never selling units held for transfer. Returns the units removed.
        /// </summary>
        public int TakeFromSlot(int buildingId, int slotIndex, int maxUnits)
        {
            var slots = GetOrCreate(buildingId);
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex].IsEmpty || maxUnits <= 0)
                return 0;

            var crop = slots[slotIndex].Type;
            int available = AvailableIn(buildingId, crop);
            int inSlot = slots[slotIndex].Count;
            int take = maxUnits;
            if (take > available) take = available;
            if (take > inSlot) take = inSlot;
            if (take <= 0)
                return 0;

            slots[slotIndex].Count -= take;
            if (slots[slotIndex].Count <= 0)
                slots[slotIndex] = default;
            ClampReservations(buildingId);
            return take;
        }
    }
}
