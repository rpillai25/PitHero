using RolePlayingFramework.Equipment;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>
    /// Service that stores all items (equipped + inventory) from fallen heroes.
    /// Items are stacked up to 999 per item type. Eventually a Second Chance merchant
    /// will allow the player to purchase these items back.
    /// </summary>
    public class SecondChanceMerchantVault
    {
        /// <summary>Represents a stacked item in the vault.</summary>
        public sealed class StackedItem
        {
            public IItem ItemTemplate { get; }
            public int Quantity { get; set; }

            public StackedItem(IItem itemTemplate, int quantity)
            {
                ItemTemplate = itemTemplate;
                Quantity = quantity;
            }
        }

        private const int MaxStackSize = 999;
        private readonly List<StackedItem> _stacks = new List<StackedItem>();

        /// <summary>Gets a read-only collection of all stacked items in the vault.</summary>
        public IReadOnlyList<StackedItem> Stacks => _stacks.AsReadOnly();

        /// <summary>Adds a single item to the vault, stacking with existing items if applicable.</summary>
        /// <param name="item">The item to add.</param>
        public void AddItem(IItem item)
        {
            if (item == null) return;

            // For consumables, get the stack count from the item
            int quantityToAdd = 1;
            if (item is Consumable consumable)
            {
                quantityToAdd = consumable.StackCount;
            }

            // Keep adding until all quantity is placed
            while (quantityToAdd > 0)
            {
                // Try to find an existing stack of the same item that has space
                StackedItem existingStack = null;
                for (int i = 0; i < _stacks.Count; i++)
                {
                    if (IsSameItem(_stacks[i].ItemTemplate, item) && _stacks[i].Quantity < MaxStackSize)
                    {
                        existingStack = _stacks[i];
                        break;
                    }
                }

                if (existingStack != null)
                {
                    // Add to existing stack (cap at MaxStackSize)
                    int availableSpace = MaxStackSize - existingStack.Quantity;
                    int amountToAdd = quantityToAdd < availableSpace ? quantityToAdd : availableSpace;
                    existingStack.Quantity += amountToAdd;
                    quantityToAdd -= amountToAdd;
                }
                else
                {
                    // Create a new stack with as much as we can fit (up to MaxStackSize)
                    int amountForNewStack = quantityToAdd < MaxStackSize ? quantityToAdd : MaxStackSize;
                    var newStack = new StackedItem(CloneItemTemplate(item), amountForNewStack);
                    _stacks.Add(newStack);
                    quantityToAdd -= amountForNewStack;
                }
            }
        }

        /// <summary>Adds multiple items to the vault.</summary>
        /// <param name="items">The items to add.</param>
        public void AddItems(IEnumerable<IItem> items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                AddItem(item);
            }
        }

        /// <summary>Removes a quantity of an item from the vault (e.g., when purchased).</summary>
        /// <param name="stack">The stack to remove from.</param>
        /// <param name="quantity">The quantity to remove.</param>
        /// <returns>True if the quantity was successfully removed.</returns>
        public bool RemoveQuantity(StackedItem stack, int quantity)
        {
            if (stack == null || quantity <= 0) return false;
            if (!_stacks.Contains(stack)) return false;
            if (stack.Quantity < quantity) return false;

            stack.Quantity -= quantity;

            // Remove the stack if it's empty
            if (stack.Quantity <= 0)
            {
                _stacks.Remove(stack);
            }

            return true;
        }

        /// <summary>Gets the total number of unique item stacks in the vault.</summary>
        public int StackCount => _stacks.Count;

        /// <summary>Gets the total quantity of all items in the vault.</summary>
        public int TotalItemCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _stacks.Count; i++)
                {
                    total += _stacks[i].Quantity;
                }
                return total;
            }
        }

        /// <summary>Clears all items from the vault.</summary>
        public void Clear()
        {
            _stacks.Clear();
        }

        /// <summary>Checks if two items are the same type (for stacking purposes).</summary>
        private bool IsSameItem(IItem item1, IItem item2)
        {
            if (item1 == null || item2 == null) return false;

            // Items are considered the same if they have the same name, kind, and rarity
            return item1.Name == item2.Name &&
                   item1.Kind == item2.Kind &&
                   item1.Rarity == item2.Rarity;
        }

        /// <summary>Creates a clean template copy of an item (for consumables, resets stack count to 0).</summary>
        private IItem CloneItemTemplate(IItem item)
        {
            // For now, just return the item as-is since IItem is typically immutable
            // For consumables, we'll use the item as a template and track quantity separately
            return item;
        }
    }
}
