namespace PitHero.UI
{
    /// <summary>Types of inventory slots in the Hero UI.</summary>
    public enum InventorySlotType
    {
        /// <summary>Empty space slot that cannot hold items.</summary>
        Null,

        /// <summary>General inventory slot for any item type.</summary>
        Inventory,

        /// <summary>Shortcut slot accessible via keyboard 1-8 keys.</summary>
        Shortcut,

        /// <summary>Equipment slot for specific gear types.</summary>
        Equipment
    }
}