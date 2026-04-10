using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero.VirtualGame;

namespace PitHero.Services
{
    /// <summary>
    /// Service that manages the player's personal crystal inventory (80 slots) and infusion queue (5 slots).
    /// </summary>
    public class CrystalCollectionService : ICrystalCollectionService
    {
        private const int MaxInventorySlots = 80;
        private const int QueueSlotCount = 5;

        private readonly HeroCrystal[] _inventory;
        private readonly HeroCrystal[] _queue;
        private readonly int[] _queueInventoryIndices;

        /// <summary>Crystal popped from queue during hero death, consumed at promotion.</summary>
        public HeroCrystal? PendingNextCrystal { get; set; }

        public int ForgeInputA { get; private set; } = -1;
        public int ForgeInputB { get; private set; } = -1;

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

        public CrystalCollectionService()
        {
            _inventory = new HeroCrystal[MaxInventorySlots];
            _queue = new HeroCrystal[QueueSlotCount];
            _queueInventoryIndices = new int[QueueSlotCount];
            for (int i = 0; i < QueueSlotCount; i++)
            {
                _queueInventoryIndices[i] = -1;
            }
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

            // Also remove from queue if it was queued
            for (int i = 0; i < _queueInventoryIndices.Length; i++)
            {
                if (_queueInventoryIndices[i] == slotIndex)
                {
                    _queue[i] = null;
                    _queueInventoryIndices[i] = -1;
                }
            }

            // Clear forge inputs if this slot was selected
            if (ForgeInputA == slotIndex)
                ForgeInputA = -1;
            if (ForgeInputB == slotIndex)
                ForgeInputB = -1;

            return true;
        }

        /// <summary>Returns the crystal at the specified inventory slot, or null if empty.</summary>
        public HeroCrystal? GetInventoryCrystal(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventory.Length)
                return null;

            return _inventory[slotIndex];
        }

        /// <summary>Sets the two forge input slots.</summary>
        public void SetForgeInput(int slotA, int slotB)
        {
            ForgeInputA = slotA;
            ForgeInputB = slotB;
        }

        /// <summary>Combines forge inputs into a combo crystal. Returns null on failure.</summary>
        public HeroCrystal TryForge(string combinedName)
        {
            if (ForgeInputA < 0 || ForgeInputB < 0)
                return null;

            var crystalA = GetInventoryCrystal(ForgeInputA);
            var crystalB = GetInventoryCrystal(ForgeInputB);

            if (crystalA == null || crystalB == null)
                return null;

            var combined = HeroCrystal.Combine(combinedName, crystalA, crystalB);

            // Remove both input crystals from inventory
            TryRemoveFromInventory(ForgeInputA);
            TryRemoveFromInventory(ForgeInputB);

            // Clear forge inputs
            ForgeInputA = -1;
            ForgeInputB = -1;

            return combined;
        }

        /// <summary>Adds a crystal to the queue at the specified slot by referencing an inventory slot.</summary>
        public bool EnqueueAt(int queueSlot, int inventoryIndex)
        {
            if (queueSlot < 0 || queueSlot >= _queue.Length)
                return false;

            if (inventoryIndex < 0 || inventoryIndex >= _inventory.Length)
                return false;

            var crystal = _inventory[inventoryIndex];
            if (crystal == null)
                return false;

            _queue[queueSlot] = crystal;
            _queueInventoryIndices[queueSlot] = inventoryIndex;
            return true;
        }

        /// <summary>Clears the queue slot at the specified index.</summary>
        public void ClearQueueSlot(int queueSlot)
        {
            if (queueSlot < 0 || queueSlot >= _queue.Length)
                return;

            _queue[queueSlot] = null;
            _queueInventoryIndices[queueSlot] = -1;
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

        /// <summary>Removes and returns the first queued crystal, removes from inventory too, shifts queue up.</summary>
        public HeroCrystal? Dequeue()
        {
            for (int i = 0; i < _queue.Length; i++)
            {
                if (_queue[i] != null)
                {
                    var crystal = _queue[i];
                    var inventoryIndex = _queueInventoryIndices[i];

                    // Remove from inventory
                    if (inventoryIndex >= 0 && inventoryIndex < _inventory.Length)
                    {
                        _inventory[inventoryIndex] = null;
                    }

                    // Shift remaining queue entries up
                    for (int j = i; j < _queue.Length - 1; j++)
                    {
                        _queue[j] = _queue[j + 1];
                        _queueInventoryIndices[j] = _queueInventoryIndices[j + 1];
                    }

                    // Clear the last slot
                    _queue[_queue.Length - 1] = null;
                    _queueInventoryIndices[_queueInventoryIndices.Length - 1] = -1;

                    return crystal;
                }
            }
            return null;
        }

        /// <summary>Adds a crystal to the back of the auto-infuse queue.</summary>
        public bool TryEnqueue(HeroCrystal crystal)
        {
            if (crystal == null)
                return false;

            // First add to inventory
            if (!TryAddToInventory(crystal))
                return false;

            // Find the inventory slot we just added to
            int inventoryIndex = -1;
            for (int i = 0; i < _inventory.Length; i++)
            {
                if (_inventory[i] == crystal)
                {
                    inventoryIndex = i;
                    break;
                }
            }

            if (inventoryIndex < 0)
                return false;

            // Find first empty queue slot
            for (int i = 0; i < _queue.Length; i++)
            {
                if (_queue[i] == null)
                {
                    _queue[i] = crystal;
                    _queueInventoryIndices[i] = inventoryIndex;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Clears the entire inventory and queue and resets PendingNextCrystal.</summary>
        public void Clear()
        {
            for (int i = 0; i < _inventory.Length; i++)
            {
                _inventory[i] = null;
            }

            for (int i = 0; i < _queue.Length; i++)
            {
                _queue[i] = null;
                _queueInventoryIndices[i] = -1;
            }

            ForgeInputA = -1;
            ForgeInputB = -1;
            PendingNextCrystal = null;
        }
    }
}
