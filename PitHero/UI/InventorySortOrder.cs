namespace PitHero.UI
{
    /// <summary>Defines the sort order for inventory items.</summary>
    public enum InventorySortOrder
    {
        /// <summary>Sort by acquisition time (AcquireIndex).</summary>
        Time,

        /// <summary>Sort by item type (Kind).</summary>
        Type,

        /// <summary>Sort by item name alphabetically.</summary>
        Name
    }
}
