namespace PitHero.Services
{
    /// <summary>
    /// Remaps hero bag slot indices written by older builds whose Party inventory grid had a
    /// different shape. Bag items are saved by linear slot index, and the grid lays those indices
    /// out row-major, so a change in column count silently moves every item that isn't in column 0.
    /// Pure integer math (AOT-safe, no allocations) so it can be applied while reading a save.
    /// </summary>
    public static class BagLayoutMigration
    {
        /// <summary>Columns of the bag grid in saves written before version 33 (24 x 5 layout).</summary>
        public const int Pre33Columns = 24;
        /// <summary>Bag rows in saves written before version 33.</summary>
        public const int Pre33Rows = 5;

        /// <summary>Columns of the current bag grid (30 x 4 layout).</summary>
        public const int CurrentColumns = 30;
        /// <summary>Bag rows of the current bag grid.</summary>
        public const int CurrentRows = 4;

        /// <summary>Slot count shared by both layouts (24 x 5 = 30 x 4 = 120).</summary>
        public const int Capacity = Pre33Columns * Pre33Rows;

        /// <summary>
        /// Maps a pre-v33 (24 x 5) bag index onto the current (30 x 4) grid so the player's
        /// arrangement survives the reshape. The first four old rows keep their exact (row, column):
        /// the new grid is wider, so those cells still exist. The vanished fifth row (24 slots) spills
        /// into the six new columns (24-29) of rows 0-3, filling them row by row. The mapping is a
        /// bijection over 0..119; indices outside that range are returned unchanged.
        /// </summary>
        public static int RemapPre33(int oldIndex)
        {
            if (oldIndex < 0 || oldIndex >= Capacity)
                return oldIndex;

            int row = oldIndex / Pre33Columns;
            int col = oldIndex % Pre33Columns;

            if (row < CurrentRows)
                return row * CurrentColumns + col;

            // Old row 4: six per new row, appended after the old 24 columns
            const int spillPerRow = CurrentColumns - Pre33Columns; // 6
            return (col / spillPerRow) * CurrentColumns + Pre33Columns + (col % spillPerRow);
        }
    }
}
