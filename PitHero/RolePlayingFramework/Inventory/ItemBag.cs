using System.Collections.Generic;
using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Inventory
{
    /// <summary>Variable-capacity item bag that can be upgraded.</summary>
    public sealed class ItemBag
    {
        private readonly List<IItem> _items;
        
        /// <summary>Current bag name.</summary>
        public string BagName { get; private set; }
        
        /// <summary>Current capacity of the bag.</summary>
        public int Capacity { get; private set; }
        
        /// <summary>Items currently in the bag.</summary>
        public IReadOnlyList<IItem> Items => _items;

        /// <summary>Current number of items in the bag.</summary>
        public int Count => _items.Count;

        /// <summary>Whether the bag is full.</summary>
        public bool IsFull => _items.Count >= Capacity;

        public ItemBag(string bagName = "Standard Bag", int capacity = 12)
        {
            BagName = bagName;
            Capacity = capacity;
            _items = new List<IItem>(capacity);
        }

        /// <summary>Adds an item if capacity allows.</summary>
        public bool TryAdd(IItem item)
        {
            if (_items.Count >= Capacity) return false;
            _items.Add(item);
            return true;
        }

        /// <summary>Removes the first matching item.</summary>
        public bool Remove(IItem item)
        {
            return _items.Remove(item);
        }

        /// <summary>Removes an item at the specified index.</summary>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            _items.RemoveAt(index);
            return true;
        }

        /// <summary>Upgrades the bag using another bag item.</summary>
        public bool TryUpgrade(IItem bagItem)
        {
            var (newCapacity, newBagName) = GetBagStats(bagItem);
            if (newCapacity <= Capacity) return false; // Can't downgrade

            Capacity = newCapacity;
            BagName = newBagName;
            return true;
        }

        /// <summary>Gets the capacity and name for a bag item.</summary>
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
    }
}