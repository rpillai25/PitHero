namespace PitHero.UI
{
    /// <summary>Defines the sort order for inventory items. Ordinals are recorded in replays (SortBag command) — never renumber.</summary>
    public enum InventorySortOrder
    {
        /// <summary>Sort by acquisition time, most recent first (ItemBag acquisition order).</summary>
        Time,

        /// <summary>Sort by item category: gear first, consumables last.</summary>
        Type,

        /// <summary>Sort by item name alphabetically.</summary>
        Name
    }
}
