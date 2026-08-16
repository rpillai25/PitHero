using System.Collections.Generic;
using PitHero.Farming;

namespace PitHero.Services
{
    /// <summary>
    /// Slot-based inventory for the kitchen refrigerator (issue #386). A single 8×4 page of
    /// <see cref="HarvestSlot"/> stacks; every stack caps at the flat
    /// <see cref="GameConfig.KitchenFridgeStackSize"/> (10 units) regardless of crop type, unlike
    /// crop storage which uses per-crop max harvest stacks. No Core.* dependencies so headless
    /// tests can construct it directly.
    /// </summary>
    public class FridgeInventoryService
    {
        /// <summary>Total fridge slots (8×4 grid, one UI page).</summary>
        public const int SlotCount = 32;

        private readonly HarvestSlot[] _slots = new HarvestSlot[SlotCount];
        private int _preStockStackSize = 1;
        private int _version;

        /// <summary>
        /// Number of 10-unit stacks of each available crop the runners try to keep stocked.
        /// Clamped to <see cref="GameConfig.KitchenPreStockStackSizeMin"/>..<see cref="GameConfig.KitchenPreStockStackSizeMax"/>. Persisted.
        /// </summary>
        public int PreStockStackSize
        {
            get => _preStockStackSize;
            set
            {
                int v = value;
                if (v < GameConfig.KitchenPreStockStackSizeMin) v = GameConfig.KitchenPreStockStackSizeMin;
                if (v > GameConfig.KitchenPreStockStackSizeMax) v = GameConfig.KitchenPreStockStackSizeMax;
                _preStockStackSize = v;
            }
        }

        /// <summary>Increments on every content mutation; the UI rebuilds its grid when this changes.</summary>
        public int Version => _version;

        /// <summary>Total units of the crop currently in the fridge.</summary>
        public int Count(CropType crop)
        {
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].Type == crop)
                    total += _slots[i].Count;
            }
            return total;
        }

        /// <summary>
        /// Adds up to <paramref name="amount"/> units of the crop, topping off existing non-full
        /// stacks first, then filling empty slots. Returns the number of units actually stored
        /// (0..amount); any remainder that doesn't fit is not stored.
        /// </summary>
        public int Deposit(CropType crop, int amount)
        {
            if (amount <= 0)
                return 0;

            int max = GameConfig.KitchenFridgeStackSize;
            int remaining = amount;

            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].Type == crop && _slots[i].Count < max)
                {
                    int room = max - _slots[i].Count;
                    int add = room < remaining ? room : remaining;
                    _slots[i].Count += add;
                    remaining -= add;
                }
            }

            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int add = max < remaining ? max : remaining;
                    _slots[i].Type = crop;
                    _slots[i].Count = add;
                    remaining -= add;
                }
            }

            int stored = amount - remaining;
            if (stored > 0)
                _version++;
            return stored;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> units of the crop, draining partial stacks
        /// before full ones so slots free up quickly. Returns the number of units actually taken.
        /// </summary>
        public int Withdraw(CropType crop, int amount)
        {
            if (amount <= 0)
                return 0;

            int remaining = amount;

            // Drain partial stacks first
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].Type == crop && _slots[i].Count < GameConfig.KitchenFridgeStackSize)
                    remaining -= TakeFromSlot(i, remaining);
            }

            // Then full stacks
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].Type == crop)
                    remaining -= TakeFromSlot(i, remaining);
            }

            int taken = amount - remaining;
            if (taken > 0)
                _version++;
            return taken;
        }

        private int TakeFromSlot(int index, int amount)
        {
            int take = _slots[index].Count < amount ? _slots[index].Count : amount;
            _slots[index].Count -= take;
            if (_slots[index].Count <= 0)
            {
                _slots[index].Type = default;
                _slots[index].Count = 0;
            }
            return take;
        }

        /// <summary>
        /// Units of the crop that could still be deposited: room left in the crop's non-full
        /// stacks plus empty slots × stack size.
        /// </summary>
        public int CapacityFor(CropType crop)
        {
            int max = GameConfig.KitchenFridgeStackSize;
            int capacity = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                    capacity += max;
                else if (_slots[i].Type == crop && _slots[i].Count < max)
                    capacity += max - _slots[i].Count;
            }
            return capacity;
        }

        /// <summary>Read-only view of the fridge slots for the UI and save gather.</summary>
        public IReadOnlyList<HarvestSlot> GetSlots() => _slots;

        /// <summary>Empties a single slot (sell / send-to-storage actions).</summary>
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
                return;
            if (_slots[slotIndex].IsEmpty)
                return;
            _slots[slotIndex].Type = default;
            _slots[slotIndex].Count = 0;
            _version++;
        }

        /// <summary>Replaces all fridge contents from save data (extra input slots are ignored).</summary>
        public void RestoreSlots(HarvestSlot[] slots)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (slots != null && i < slots.Length)
                    _slots[i] = slots[i];
                else
                {
                    _slots[i].Type = default;
                    _slots[i].Count = 0;
                }
            }
            _version++;
        }

        /// <summary>Empties the fridge (new game / scene reset).</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Type = default;
                _slots[i].Count = 0;
            }
            _version++;
        }
    }
}
