using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.Services
{
    /// <summary>
    /// The one definition of the Party window grid geometry: 30 columns by 7 rows, a name row, two
    /// equipment rows (first mercenary block, hero block, second mercenary block) and four bag rows.
    /// Synergy patterns are matched against this grid, so the simulation (HeroSynergyResolver) and
    /// the UI (InventoryGrid) must agree on it cell for cell — both read these constants.
    /// Only the bag rows take part in synergy detection: equipment cells, hero or mercenary, never do.
    /// </summary>
    public static class PartyGridLayout
    {
        public const int Width = 30;
        public const int Height = 7;             // 1 name row + 2 equipment rows + 4 bag rows
        public const int BagRowStart = 3;
        public const int BagRows = Height - BagRowStart;
        public const int BagCapacity = Width * BagRows;

        public const int EquipBlockStart = (Width - 11) / 2;   // 9
        public const int Merc0Col = EquipBlockStart;            // first mercenary, left of the hero
        public const int HeroCol = EquipBlockStart + 4;         // hero equipment columns
        public const int Merc1Col = EquipBlockStart + 8;        // second mercenary, right of the hero
        public const int MaxMercenaries = 2;

        /// <summary>Bag slot index for a grid cell, or -1 when the cell is not a bag cell.</summary>
        public static int CellToBagIndex(int x, int y)
        {
            if (y < BagRowStart || y >= Height || x < 0 || x >= Width)
                return -1;
            return (y - BagRowStart) * Width + x;
        }

        /// <summary>Grid cell for a bag slot index (1:1 row-major mapping under the equipment rows).</summary>
        public static Point BagIndexToCell(int bagIndex)
        {
            return new Point(bagIndex % Width, BagRowStart + bagIndex / Width);
        }

        /// <summary>
        /// Fills <paramref name="grid"/> (Width x Height, cleared first) with the bag's items in their
        /// bag rows. The name and equipment rows stay empty by design (SynergySystem.md: synergies are
        /// arrangements in the hero's inventory).
        /// </summary>
        public static void FillSynergyGrid(IItem[,] grid, ItemBag bag)
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    grid[x, y] = null;

            if (bag == null)
                return;
            int capacity = bag.Capacity < BagCapacity ? bag.Capacity : BagCapacity;
            for (int i = 0; i < capacity; i++)
            {
                var item = bag.GetSlotItem(i);
                if (item == null) continue;
                var cell = BagIndexToCell(i);
                grid[cell.X, cell.Y] = item;
            }
        }
    }
}
