using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Manages selection and swapping between InventoryGrid and ShortcutBar
    /// </summary>
    public static class InventorySelectionManager
    {
        private static InventorySlot _selectedSlot;
        private static bool _isFromShortcutBar;
        private static HeroComponent _heroComponent;
        
        /// <summary>Callback to refresh inventory grid after cross-component swap</summary>
        public static System.Action OnInventoryChanged;
        
        /// <summary>Sets the selected slot from inventory grid</summary>
        public static void SetSelectedFromInventory(InventorySlot slot, HeroComponent hero)
        {
            ClearSelection();
            _selectedSlot = slot;
            _isFromShortcutBar = false;
            _heroComponent = hero;
            if (slot != null)
                slot.SlotData.IsHighlighted = true;
        }
        
        /// <summary>Sets the selected slot from shortcut bar</summary>
        public static void SetSelectedFromShortcut(InventorySlot slot, HeroComponent hero)
        {
            ClearSelection();
            _selectedSlot = slot;
            _isFromShortcutBar = true;
            _heroComponent = hero;
            if (slot != null)
                slot.SlotData.IsHighlighted = true;
        }
        
        /// <summary>Clears the current selection</summary>
        public static void ClearSelection()
        {
            if (_selectedSlot != null)
            {
                _selectedSlot.SlotData.IsHighlighted = false;
                _selectedSlot = null;
            }
            _isFromShortcutBar = false;
            _heroComponent = null;
        }
        
        /// <summary>Gets the currently selected slot</summary>
        public static InventorySlot GetSelectedSlot() => _selectedSlot;
        
        /// <summary>Returns true if the selected slot is from shortcut bar</summary>
        public static bool IsSelectionFromShortcutBar() => _isFromShortcutBar;
        
        /// <summary>Returns true if there is a selected slot</summary>
        public static bool HasSelection() => _selectedSlot != null;
        
        /// <summary>Attempts to swap between inventory and shortcut slot</summary>
        public static bool TrySwapCrossComponent(InventorySlot targetSlot, bool targetIsShortcut, HeroComponent targetHero)
        {
            if (_selectedSlot == null || _heroComponent == null)
                return false;
                
            // Only allow swap if they're from different components
            if (_isFromShortcutBar == targetIsShortcut)
                return false;
                
            // Determine which is inventory and which is shortcut
            InventorySlot inventorySlot = _isFromShortcutBar ? targetSlot : _selectedSlot;
            InventorySlot shortcutSlot = _isFromShortcutBar ? _selectedSlot : targetSlot;
            
            if (!inventorySlot.SlotData.BagIndex.HasValue || !shortcutSlot.SlotData.BagIndex.HasValue)
                return false;
                
            // Get the items
            IItem inventoryItem = inventorySlot.SlotData.Item;
            IItem shortcutItem = shortcutSlot.SlotData.Item;
            
            int inventoryBagIndex = inventorySlot.SlotData.BagIndex.Value;
            int shortcutBagIndex = shortcutSlot.SlotData.BagIndex.Value;
            
            // Swap in the bags
            _heroComponent.Bag.SetSlotItem(inventoryBagIndex, shortcutItem);
            _heroComponent.ShortcutBag.SetSlotItem(shortcutBagIndex, inventoryItem);
            
            ClearSelection();
            
            // Trigger refresh callback
            OnInventoryChanged?.Invoke();
            
            return true;
        }
    }
}
