using PitHero.UI;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>Marks which hero bag slots are covered by placed stencils (cells the player arranged by hand).</summary>
    public static class StencilBagMask
    {
        /// <summary>
        /// Clears <paramref name="mask"/> and sets every bag index covered by a cell of a placed stencil.
        /// bagIndex = (gridY - BagRowStart) * BagColumns + gridX; cells off the bag rows are ignored.
        /// </summary>
        public static void Build(IReadOnlyList<PlacedStencilRecord> placedStencils, bool[] mask)
        {
            if (mask == null) return;
            System.Array.Clear(mask, 0, mask.Length);
            if (placedStencils == null) return;

            int lastBagRow = InventoryGrid.BagRowStart + InventoryGrid.BagRows - 1;
            for (int s = 0; s < placedStencils.Count; s++)
            {
                var record = placedStencils[s];
                var pattern = SynergyPatternRegistry.GetById(record.PatternId);
                if (pattern == null) continue;

                var offsets = pattern.GridOffsets;
                for (int i = 0; i < offsets.Count; i++)
                {
                    int gridX = record.AnchorX + offsets[i].X;
                    int gridY = record.AnchorY + offsets[i].Y;
                    if (gridY < InventoryGrid.BagRowStart || gridY > lastBagRow) continue;
                    if (gridX < 0 || gridX >= InventoryGrid.BagColumns) continue;

                    int bagIndex = (gridY - InventoryGrid.BagRowStart) * InventoryGrid.BagColumns + gridX;
                    if (bagIndex < mask.Length) mask[bagIndex] = true;
                }
            }
        }
    }
}
