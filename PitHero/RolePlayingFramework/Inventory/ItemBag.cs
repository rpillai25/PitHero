using System.Collections.Generic;
using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Inventory
{
    /// <summary>Variable-capacity item bag with fixed slot array preserving ordering.</summary>
    public sealed class ItemBag
    {
        private IItem[] _slots;                    // fixed-size slot array (can contain nulls)
        private int _count;                        // number of non-null items
        private readonly List<IItem> _compact;     // reusable compact non-null list for Items exposure
        private bool _compactDirty;                // flag to rebuild compact list lazily

        /// <summary>Current bag name.</summary>
        public string BagName { get; private set; }
        /// <summary>Number of slots (capacity).</summary>
        public int Capacity { get; private set; }
        /// <summary>Number of non-null items.</summary>
        public int Count => _count;
        /// <summary>True when no empty slots remain.</summary>
        public bool IsFull => _count >= Capacity;
        /// <summary>Non-null items in insertion/slot order (reused list, do not modify).</summary>
        public IReadOnlyList<IItem> Items { get { EnsureCompact(); return _compact; } }

        public ItemBag(string bagName = "Standard Bag", int capacity = 12)
        {
            BagName = bagName;
            Capacity = capacity;
            _slots = new IItem[capacity];
            _count = 0;
            _compact = new List<IItem>(capacity);
            _compactDirty = true;
        }

        /// <summary>Adds an item to first empty slot.</summary>
        public bool TryAdd(IItem item)
        {
            if (item == null || IsFull) return false;
            
            // If it's a consumable, try to stack it first
            if (item is Consumable consumable)
            {
                // Look for an existing stack of the same item that isn't maxed out
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] is Consumable existingConsumable &&
                        existingConsumable.Name == consumable.Name &&
                        existingConsumable.StackCount < existingConsumable.StackSize)
                    {
                        existingConsumable.StackCount++;
                        _compactDirty = true;
                        return true;
                    }
                }
            }
            
            // If not stackable or no existing stack found, add to first empty slot
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = item;
                    _count++;
                    _compactDirty = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Removes first matching item (clears its slot).</summary>
        public bool Remove(IItem item)
        {
            if (item == null) return false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == item)
                {
                    _slots[i] = null;
                    _count--;
                    _compactDirty = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Consumes one item from a consumable stack at the given slot index. Returns true if consumed, false if slot is empty or item is gone.</summary>
        public bool ConsumeFromStack(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
            var item = _slots[slotIndex];
            if (item is Consumable consumable)
            {
                consumable.StackCount--;
                if (consumable.StackCount <= 0)
                {
                    _slots[slotIndex] = null;
                    _count--;
                    _compactDirty = true;
                }
                return true;
            }
            return false;
        }

        /// <summary>Removes the item at the logical compact index (Nth non-null slot).</summary>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _count) return false;
            int seen = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                {
                    if (seen == index)
                    {
                        _slots[i] = null;
                        _count--;
                        _compactDirty = true;
                        return true;
                    }
                    seen++;
                }
            }
            return false;
        }

        /// <summary>Upgrades bag capacity preserving slot ordering.</summary>
        public bool TryUpgrade(IItem bagItem)
        {
            var (newCapacity, newBagName) = GetBagStats(bagItem);
            if (newCapacity <= Capacity) return false;
            var newSlots = new IItem[newCapacity];
            for (int i = 0; i < _slots.Length; i++)
                newSlots[i] = _slots[i];
            _slots = newSlots;
            Capacity = newCapacity;
            BagName = newBagName;
            _compactDirty = true;
            return true;
        }

        /// <summary>Gets capacity and name metadata for a bag item.</summary>
        public static (int capacity, string name) GetBagStats(IItem bagItem)
        {
            return bagItem.Name.ToLower() switch
            {
                "standard bag" => (12, "Standard Bag"),
                "forager's bag" => (16, "Forager's Bag"),
                "traveller's bag" => (20, "Traveller's Bag"),
                "adventurer's bag" => (24, "Adventurer's Bag"),
                "merchant's bag" => (32, "Merchant's Bag"),
                _ => (12, "Standard Bag")
            };
        }

        /// <summary>Gets item at slot index (can be null).</summary>
        public IItem GetSlotItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return null;
            return _slots[slotIndex];
        }

        /// <summary>Swaps two slot indices.</summary>
        public bool SwapSlots(int indexA, int indexB)
        {
            if (indexA == indexB) return true;
            if (indexA < 0 || indexB < 0) return false;
            if (indexA >= _slots.Length || indexB >= _slots.Length) return false;
            var tmp = _slots[indexA];
            _slots[indexA] = _slots[indexB];
            _slots[indexB] = tmp;
            _compactDirty = true;
            return true;
        }

        /// <summary>Sets item at slot index (null allowed).</summary>
        public bool SetSlotItem(int slotIndex, IItem item)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
            var existing = _slots[slotIndex];
            if (existing == item) return true;
            if (existing != null) { _count--; }
            if (item != null) { _count++; }
            _slots[slotIndex] = item;
            _compactDirty = true;
            return true;
        }

        /// <summary>Replaces slot ordering with provided list (length <= capacity, nulls allowed).</summary>
        public void SetItemsInOrder(List<IItem> ordered)
        {
            if (ordered == null) return;
            int len = ordered.Count;
            if (len > _slots.Length) len = _slots.Length;
            for (int i = 0; i < len; i++) _slots[i] = ordered[i];
            for (int i = len; i < _slots.Length; i++) _slots[i] = null;
            // recalc count
            _count = 0;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) _count++;
            _compactDirty = true;
        }

        /// <summary>Replaces slot ordering with provided raw buffer (no allocation path).</summary>
        public void SetItemsInOrder(IItem[] orderedBuffer, int count)
        {
            if (orderedBuffer == null) return;
            if (count > orderedBuffer.Length) count = orderedBuffer.Length;
            if (count > _slots.Length) count = _slots.Length;
            for (int i = 0; i < count; i++) _slots[i] = orderedBuffer[i];
            for (int i = count; i < _slots.Length; i++) _slots[i] = null;
            _count = 0;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) _count++;
            _compactDirty = true;
        }

        /// <summary>Ensures compact non-null list is synchronized.</summary>
        private void EnsureCompact()
        {
            if (!_compactDirty) return;
            _compact.Clear();
            for (int i = 0; i < _slots.Length; i++)
            {
                var it = _slots[i];
                if (it != null) _compact.Add(it);
            }
            _compactDirty = false;
        }
    }
}