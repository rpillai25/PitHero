using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;
using RolePlayingFramework.Synergies;

namespace PitHero.Tests
{
    /// <summary>
    /// Every registered synergy stencil has to be placeable in the bag area of the inventory grid.
    /// The grid is deliberately wide and short, so a tall pattern would be impossible to place.
    /// </summary>
    [TestClass]
    public class SynergyPatternFitTests
    {
        [TestMethod]
        public void EveryPattern_FitsTheBagArea()
        {
            var patterns = SynergyPatternRegistry.All;
            Assert.IsTrue(patterns.Count > 0, "registry should not be empty");

            for (int i = 0; i < patterns.Count; i++)
            {
                var pattern = patterns[i];
                var offsets = pattern.GridOffsets;
                Assert.IsTrue(offsets.Count > 0, $"{pattern.Id} has no cells");

                int minX = offsets[0].X, maxX = offsets[0].X;
                int minY = offsets[0].Y, maxY = offsets[0].Y;
                for (int c = 1; c < offsets.Count; c++)
                {
                    if (offsets[c].X < minX) minX = offsets[c].X;
                    if (offsets[c].X > maxX) maxX = offsets[c].X;
                    if (offsets[c].Y < minY) minY = offsets[c].Y;
                    if (offsets[c].Y > maxY) maxY = offsets[c].Y;
                }

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;

                Assert.IsTrue(height <= InventoryGrid.BagRows,
                    $"{pattern.Id} is {width}x{height}; the bag is only {InventoryGrid.BagRows} rows tall");
                Assert.IsTrue(width <= InventoryGrid.BagColumns,
                    $"{pattern.Id} is {width}x{height}; the bag is only {InventoryGrid.BagColumns} columns wide");
            }
        }

        [TestMethod]
        public void EveryPattern_HasOneRequiredKindPerCell()
        {
            var patterns = SynergyPatternRegistry.All;
            for (int i = 0; i < patterns.Count; i++)
            {
                var pattern = patterns[i];
                Assert.AreEqual(pattern.GridOffsets.Count, pattern.RequiredKinds.Count,
                    $"{pattern.Id} has {pattern.GridOffsets.Count} cells but {pattern.RequiredKinds.Count} required kinds");
            }
        }
    }
}
