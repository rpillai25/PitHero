using System.Collections.Generic;
using RolePlayingFramework.Equipment;

namespace PitHero.Services
{
    /// <summary>
    /// Service that stores inventory items from fallen heroes.
    /// Eventually a Pit Merchant will sell these items back to the player.
    /// </summary>
    public class PitMerchantVault
    {
        private readonly List<IItem> _items = new List<IItem>();
        
        /// <summary>Gets a read-only collection of all items in the vault.</summary>
        public IReadOnlyList<IItem> Items => _items.AsReadOnly();
        
        /// <summary>Adds an item to the vault.</summary>
        /// <param name="item">The item to add.</param>
        public void AddItem(IItem item)
        {
            if (item != null)
            {
                _items.Add(item);
            }
        }
        
        /// <summary>Adds multiple items to the vault.</summary>
        /// <param name="items">The items to add.</param>
        public void AddItems(IEnumerable<IItem> items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        _items.Add(item);
                    }
                }
            }
        }
        
        /// <summary>Removes an item from the vault (e.g., when sold).</summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>True if the item was found and removed.</returns>
        public bool RemoveItem(IItem item)
        {
            return _items.Remove(item);
        }
        
        /// <summary>Gets the total number of items in the vault.</summary>
        public int Count => _items.Count;
        
        /// <summary>Clears all items from the vault.</summary>
        public void Clear()
        {
            _items.Clear();
        }
    }
}
