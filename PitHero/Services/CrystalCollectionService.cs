using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero.VirtualGame;

namespace PitHero.Services
{
    /// <summary>Identifies a logical crystal slot type for use with SwapSlots.</summary>
    public enum CrystalSlotType { Inventory, ForgeA, ForgeB, Queue }

    /// <summary>
    /// Service that manages the player's personal crystal inventory (80 slots), forge slots (2),
    /// and infusion queue (5 slots). All slots hold crystals directly — no index references.
    /// Moving a crystal between slots physically removes it from the source and places it in the
    /// destination, preventing duplicates or exploit scenarios.
    /// </summary>
    public class CrystalCollectionService : ICrystalCollectionService
    {
        private const int MaxInventorySlots = 80;
        private const int QueueSlotCount = 5;

        private readonly HeroCrystal[] _inventory;
        private readonly HeroCrystal[] _queue;
        private HeroCrystal _forgeSlotA;
        private HeroCrystal _forgeSlotB;

        /// <summary>Crystal popped from queue during hero death, consumed at promotion.</summary>
        public HeroCrystal? PendingNextCrystal { get; set; }

        /// <summary>Crystal currently in Forge slot A, or null if empty.</summary>
        public HeroCrystal ForgeSlotA => _forgeSlotA;

        /// <summary>Crystal currently in Forge slot B, or null if empty.</summary>
        public HeroCrystal ForgeSlotB => _forgeSlotB;

        public int InventoryCapacity => MaxInventorySlots;
        public int QueueCapacity => QueueSlotCount;

        public int InventoryCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _inventory.Length; i++)
                {
                    if (_inventory[i] != null)
                        count++;
                }
                return count;
            }
        }

        public int QueueCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _queue.Length; i++)
                {
                    if (_queue[i] != null)
                        count++;
                }
                return count;
            }
        }

        public IReadOnlyList<HeroCrystal> Inventory => _inventory;
        public IReadOnlyList<HeroCrystal> Queue => _queue;

        /// <summary>Directly assigns a crystal to forge slot A without touching inventory. Used for save/load restoration.</summary>
        public void SetForgeSlotADirect(HeroCrystal crystal) => _forgeSlotA = crystal;

        /// <summary>Directly assigns a crystal to forge slot B without touching inventory. Used for save/load restoration.</summary>
        public void SetForgeSlotBDirect(HeroCrystal crystal) => _forgeSlotB = crystal;

        public CrystalCollectionService()
        {
            _inventory = new HeroCrystal[MaxInventorySlots];
            _queue = new HeroCrystal[QueueSlotCount];
        }

        /// <summary>Attempts to add a crystal to the first available inventory slot.</summary>
        public bool TryAddToInventory(HeroCrystal crystal)
        {
            if (crystal == null)
                return false;

            for (int i = 0; i < _inventory.Length; i++)
            {
                if (_inventory[i] == null)
                {
                    _inventory[i] = crystal;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Removes the crystal at the specified inventory slot.</summary>
        public bool TryRemoveFromInventory(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventory.Length)
                return false;

            if (_inventory[slotIndex] == null)
                return false;

            _inventory[slotIndex] = null;
            return true;
        }

        /// <summary>Returns the crystal at the specified inventory slot, or null if empty.</summary>
        public HeroCrystal? GetInventoryCrystal(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventory.Length)
                return null;

            return _inventory[slotIndex];
        }

        /// <summary>
        /// Swaps the crystal contents of two logical slots. If only the source has a crystal it simply
        /// moves; if both have crystals they are exchanged. Silently does nothing for invalid indices
        /// or when source and destination are the same slot.
        /// </summary>
        public void SwapSlots(CrystalSlotType srcType, int srcIdx, CrystalSlotType dstType, int dstIdx)
        {
            // Prevent no-op swap with self
            if (srcType == dstType && srcIdx == dstIdx) return;

            var srcCrystal = GetSlotCrystal(srcType, srcIdx);
            var dstCrystal = GetSlotCrystal(dstType, dstIdx);

            // Prevent placing the same crystal reference in two slots
            if (srcCrystal != null && srcCrystal == dstCrystal) return;

            SetSlotCrystal(srcType, srcIdx, dstCrystal);
            SetSlotCrystal(dstType, dstIdx, srcCrystal);
        }

        /// <summary>Returns the crystal at the specified logical slot, or null if empty or index is invalid.</summary>
        private HeroCrystal GetSlotCrystal(CrystalSlotType type, int idx)
        {
            switch (type)
            {
                case CrystalSlotType.Inventory:
                    if (idx < 0 || idx >= _inventory.Length) return null;
                    return _inventory[idx];
                case CrystalSlotType.ForgeA: return _forgeSlotA;
                case CrystalSlotType.ForgeB: return _forgeSlotB;
                case CrystalSlotType.Queue:
                    if (idx < 0 || idx >= _queue.Length) return null;
                    return _queue[idx];
                default: return null;
            }
        }

        /// <summary>Sets the crystal at the specified logical slot; silently ignores invalid inventory/queue indices.</summary>
        private void SetSlotCrystal(CrystalSlotType type, int idx, HeroCrystal crystal)
        {
            switch (type)
            {
                case CrystalSlotType.Inventory:
                    if (idx >= 0 && idx < _inventory.Length) _inventory[idx] = crystal;
                    break;
                case CrystalSlotType.ForgeA: _forgeSlotA = crystal; break;
                case CrystalSlotType.ForgeB: _forgeSlotB = crystal; break;
                case CrystalSlotType.Queue:
                    if (idx >= 0 && idx < _queue.Length) _queue[idx] = crystal;
                    break;
            }
        }

        /// <summary>Combines forge slots A and B into a combo crystal. Returns null on failure.</summary>
        public HeroCrystal TryForge(string combinedName)
        {
            if (_forgeSlotA == null || _forgeSlotB == null)
                return null;

            var combined = HeroCrystal.Combine(combinedName, _forgeSlotA, _forgeSlotB);

            // Consume both forge inputs
            _forgeSlotA = null;
            _forgeSlotB = null;

            return combined;
        }

        /// <summary>Adds a crystal directly to the back of the auto-infuse queue (does not touch inventory).</summary>
        public bool TryEnqueue(HeroCrystal crystal)
        {
            if (crystal == null)
                return false;

            for (int i = 0; i < _queue.Length; i++)
            {
                if (_queue[i] == null)
                {
                    _queue[i] = crystal;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Clears the queue slot at the specified index, returning the crystal or null.</summary>
        public void ClearQueueSlot(int queueSlot)
        {
            if (queueSlot < 0 || queueSlot >= _queue.Length)
                return;
            _queue[queueSlot] = null;
        }

        /// <summary>Removes and returns the first queued crystal, shifts queue up.</summary>
        public HeroCrystal? Dequeue()
        {
            for (int i = 0; i < _queue.Length; i++)
            {
                if (_queue[i] != null)
                {
                    var crystal = _queue[i];

                    // Shift remaining queue entries up
                    for (int j = i; j < _queue.Length - 1; j++)
                        _queue[j] = _queue[j + 1];

                    // Clear the last slot
                    _queue[_queue.Length - 1] = null;

                    return crystal;
                }
            }
            return null;
        }

        /// <summary>Returns the crystal at the front of the queue without removing it.</summary>
        public HeroCrystal? PeekQueue()
        {
            for (int i = 0; i < _queue.Length; i++)
            {
                if (_queue[i] != null)
                    return _queue[i];
            }
            return null;
        }

        /// <summary>Swaps two inventory slots.</summary>
        public void SwapInventorySlots(int a, int b)
        {
            if (a < 0 || a >= _inventory.Length || b < 0 || b >= _inventory.Length || a == b)
                return;

            var tmp = _inventory[a];
            _inventory[a] = _inventory[b];
            _inventory[b] = tmp;
        }

        /// <summary>Clears the entire inventory, queue, forge slots, and resets PendingNextCrystal.</summary>
        public void Clear()
        {
            for (int i = 0; i < _inventory.Length; i++)
                _inventory[i] = null;

            for (int i = 0; i < _queue.Length; i++)
                _queue[i] = null;

            _forgeSlotA = null;
            _forgeSlotB = null;
            PendingNextCrystal = null;
        }
    }
}
