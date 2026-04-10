using RolePlayingFramework.Heroes;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// In-memory implementation of ICrystalCollectionService for use in VGL tests.
    /// Provides the full 80-slot inventory and 5-slot circular queue without any Nez or
    /// graphics dependencies, making it usable in headless unit tests.
    /// </summary>
    public class MockCrystalCollectionService : ICrystalCollectionService
    {
        private const int DefaultInventoryCapacity = 80;
        private const int DefaultQueueCapacity = 5;

        private readonly HeroCrystal?[] _inventory;
        private readonly HeroCrystal?[] _queue;
        private int _queueHead;
        private int _queueCount;

        /// <inheritdoc/>
        public int InventoryCapacity => _inventory.Length;

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public int QueueCapacity => _queue.Length;

        /// <inheritdoc/>
        public int QueueCount => _queueCount;

        /// <inheritdoc/>
        public HeroCrystal? PendingNextCrystal { get; set; }

        /// <summary>Creates a new mock service with default capacity (80 inventory, 5 queue).</summary>
        public MockCrystalCollectionService()
        {
            _inventory = new HeroCrystal?[DefaultInventoryCapacity];
            _queue = new HeroCrystal?[DefaultQueueCapacity];
            _queueHead = 0;
            _queueCount = 0;
        }

        /// <summary>Creates a new mock service with custom capacity, useful for boundary tests.</summary>
        public MockCrystalCollectionService(int inventoryCapacity, int queueCapacity)
        {
            _inventory = new HeroCrystal?[inventoryCapacity];
            _queue = new HeroCrystal?[queueCapacity];
            _queueHead = 0;
            _queueCount = 0;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool TryRemoveFromInventory(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventory.Length)
                return false;
            if (_inventory[slotIndex] == null)
                return false;

            _inventory[slotIndex] = null;
            return true;
        }

        /// <inheritdoc/>
        public HeroCrystal? GetInventoryCrystal(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventory.Length)
                return null;
            return _inventory[slotIndex];
        }

        /// <inheritdoc/>
        public bool TryEnqueue(HeroCrystal crystal)
        {
            if (crystal == null)
                return false;
            if (_queueCount >= _queue.Length)
                return false;

            int tail = (_queueHead + _queueCount) % _queue.Length;
            _queue[tail] = crystal;
            _queueCount++;
            return true;
        }

        /// <inheritdoc/>
        public HeroCrystal? Dequeue()
        {
            if (_queueCount == 0)
                return null;

            HeroCrystal? crystal = _queue[_queueHead];
            _queue[_queueHead] = null;
            _queueHead = (_queueHead + 1) % _queue.Length;
            _queueCount--;
            return crystal;
        }

        /// <inheritdoc/>
        public HeroCrystal? PeekQueue()
        {
            if (_queueCount == 0)
                return null;
            return _queue[_queueHead];
        }

        /// <inheritdoc/>
        public void Clear()
        {
            for (int i = 0; i < _inventory.Length; i++)
                _inventory[i] = null;

            for (int i = 0; i < _queue.Length; i++)
                _queue[i] = null;

            _queueHead = 0;
            _queueCount = 0;
            PendingNextCrystal = null;
        }
    }
}
