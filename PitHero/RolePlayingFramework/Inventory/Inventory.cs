using System.Collections.Generic;
using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Inventory
{
    /// <summary>Simple inventory list with capacity.</summary>
    public sealed class Inventory
    {
        private readonly List<IItem> _items;
        public int Capacity { get; }
        public IReadOnlyList<IItem> Items => _items;

        public Inventory(int capacity = 32)
        {
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
    }
}
