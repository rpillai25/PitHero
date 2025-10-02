using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>Data structure representing a single inventory slot.</summary>
    public class InventorySlotData
    {
        /// <summary>Grid X coordinate (0-based).</summary>
        public int X { get; set; }
        
        /// <summary>Grid Y coordinate (0-based).</summary>
        public int Y { get; set; }
        
        /// <summary>Type of slot.</summary>
        public InventorySlotType SlotType { get; set; }
        
        /// <summary>Equipment slot type (only used for Equipment slots).</summary>
        public EquipmentSlot? EquipmentSlot { get; set; }
        
        /// <summary>Shortcut key number (1-8, only used for Shortcut slots).</summary>
        public int? ShortcutKey { get; set; }
        
        /// <summary>Item currently in this slot.</summary>
        public IItem Item { get; set; }
        
        /// <summary>Bag index for shortcut/inventory slots (for 1:1 mapping).</summary>
        public int? BagIndex { get; set; }
        
        /// <summary>Stack count for stackable items (consumables).</summary>
        public int StackCount { get; set; }
        
        /// <summary>Whether this slot is currently highlighted.</summary>
        public bool IsHighlighted { get; set; }
        
        /// <summary>Whether this slot is currently being hovered.</summary>
        public bool IsHovered { get; set; }
        
        /// <summary>Acquisition order index (higher means more recently acquired/stacked).</summary>
        public int AcquireIndex { get; set; }

        public InventorySlotData(int x, int y, InventorySlotType slotType)
        {
            X = x;
            Y = y;
            SlotType = slotType;
        }
    }
}