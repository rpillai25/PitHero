using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero;
using PitHero.UI;

namespace PitHero.Tests
{
    /// <summary>
    /// The till / restore-grass brush footprint (issue #422). Odd sizes centre on the cursor tile,
    /// even sizes anchor it at the top-left, and the footprint always covers Size x Size tiles.
    /// </summary>
    [TestClass]
    public class TileBrushTests
    {
        [TestMethod]
        [TestCategory("UI")]
        public void DefaultBrush_CoversOnlyTheCursorTile()
        {
            var brush = new TileBrush(null, "test");
            var cursor = new Point(130, 4);

            Assert.AreEqual(1, brush.Size, "a fresh brush is a single tile");
            Assert.AreEqual(1, brush.TileCount);
            Assert.AreEqual(cursor, brush.GetOrigin(cursor), "a 1x1 footprint starts on the cursor");
            Assert.AreEqual(cursor, brush.GetTile(cursor, 0));
        }

        [TestMethod]
        [TestCategory("UI")]
        public void Footprint_CoversEveryTileExactlyOnceAtEverySize()
        {
            var cursor = new Point(130, 4);
            for (int size = 1; size <= GameConfig.TileBrushMaxSize; size++)
            {
                var brush = MakeBrushOfSize(size);
                var origin = brush.GetOrigin(cursor);
                var seen = new System.Collections.Generic.HashSet<Point>();

                Assert.AreEqual(size * size, brush.TileCount, $"size {size} covers {size * size} tiles");
                for (int i = 0; i < brush.TileCount; i++)
                {
                    var tile = brush.GetTile(cursor, i);
                    Assert.IsTrue(seen.Add(tile), $"size {size}: tile ({tile.X},{tile.Y}) enumerated twice");
                    Assert.IsTrue(tile.X >= origin.X && tile.X < origin.X + size,
                        $"size {size}: tile X {tile.X} outside the footprint");
                    Assert.IsTrue(tile.Y >= origin.Y && tile.Y < origin.Y + size,
                        $"size {size}: tile Y {tile.Y} outside the footprint");
                }
            }
        }

        [TestMethod]
        [TestCategory("UI")]
        public void EvenSizes_AnchorTheCursorAtTheTopLeft_OddSizesCentreIt()
        {
            var cursor = new Point(130, 4);

            Assert.AreEqual(cursor, MakeBrushOfSize(2).GetOrigin(cursor),
                "a 2x2 footprint grows right and down from the cursor");
            Assert.AreEqual(new Point(cursor.X - 1, cursor.Y - 1), MakeBrushOfSize(3).GetOrigin(cursor),
                "a 3x3 footprint is centred on the cursor");
        }

        [TestMethod]
        [TestCategory("UI")]
        public void FootprintCoversTheCursorTileAtEverySize()
        {
            var cursor = new Point(130, 4);
            for (int size = 1; size <= GameConfig.TileBrushMaxSize; size++)
            {
                var brush = MakeBrushOfSize(size);
                bool found = false;
                for (int i = 0; i < brush.TileCount && !found; i++)
                    found = brush.GetTile(cursor, i) == cursor;
                Assert.IsTrue(found, $"size {size}: the tile under the cursor must always be painted");
            }
        }

        [TestMethod]
        [TestCategory("UI")]
        public void Size_ClampsToTheSupportedRange()
        {
            var brush = new TileBrush(null, "test");

            brush.Size = 0;
            Assert.AreEqual(1, brush.Size, "the brush never shrinks below a single tile");
            brush.Size = -4;
            Assert.AreEqual(1, brush.Size);
            brush.Size = GameConfig.TileBrushMaxSize + 5;
            Assert.AreEqual(GameConfig.TileBrushMaxSize, brush.Size, "the brush never grows past the cap");
        }

        private static TileBrush MakeBrushOfSize(int size)
            => new TileBrush(null, "test") { Size = size };
    }
}
