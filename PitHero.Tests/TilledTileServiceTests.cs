using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez.Tiled;
using PitHero;
using PitHero.Farming;
using PitHero.Services;

namespace PitHero.Tests
{
    [TestClass]
    public class TilledTileServiceTests
    {
        private TmxMap _map;
        private TmxLayer _baseLayer;
        private TileStateService _tileState;
        private TilledTileService _service;

        private const int W = 16;
        private const int H = 12;

        [TestInitialize]
        public void Setup()
        {
            _map = new TmxMap
            {
                Layers = new TmxList<ITmxLayer>(),
                Tilesets = new TmxList<TmxTileset>()
            };
            _map.Tilesets.Add(new TmxTileset
            {
                Name = "TestTiles",
                FirstGid = 1,
                Tiles = new Dictionary<int, TmxTilesetTile>()
            });
            _baseLayer = new TmxLayer
            {
                Name = "Base",
                Map = _map,
                Width = W,
                Height = H,
                Grid = new uint[W * H],
                Tiles = new Dictionary<uint, TmxLayerTile>()
            };
            _map.Layers.Add(_baseLayer);

            _tileState = new TileStateService();
            _service = new TilledTileService(_map, _tileState);
        }

        private int GidAt(int x, int y) => (int)_baseLayer.Grid[x + y * W];

        [TestMethod]
        public void TillTile_SetsTilledFlag_AndClearsReadyToTill()
        {
            var tile = new Point(5, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);

            _service.TillTile(tile);

            Assert.IsTrue(_tileState.HasFlag(tile, TileStateFlag.Tilled));
            Assert.IsFalse(_tileState.HasFlag(tile, TileStateFlag.ReadyToTill));
        }

        [TestMethod]
        public void TillTile_IsolatedTile_WritesZerothGid()
        {
            _service.TillTile(new Point(5, 5));

            Assert.AreEqual(GameConfig.TillZerothGid, GidAt(5, 5));
        }

        [TestMethod]
        public void TillTile_AdjacentTiles_GetConnectedBitmaskGids()
        {
            // Till (5,5) then its east neighbor (6,5)
            _service.TillTile(new Point(5, 5));
            _service.TillTile(new Point(6, 5));

            // (5,5) has an east neighbor -> bitmask +4; (6,5) has a west neighbor -> bitmask +2
            Assert.AreEqual(GameConfig.TillZerothGid + 4, GidAt(5, 5));
            Assert.AreEqual(GameConfig.TillZerothGid + 2, GidAt(6, 5));
        }

        [TestMethod]
        public void TillTile_FourNeighbors_CenterGetsFullBitmask()
        {
            _service.TillTile(new Point(5, 4));   // north
            _service.TillTile(new Point(4, 5));   // west
            _service.TillTile(new Point(6, 5));   // east
            _service.TillTile(new Point(5, 6));   // south
            _service.TillTile(new Point(5, 5));   // center

            Assert.AreEqual(GameConfig.TillZerothGid + 15, GidAt(5, 5));
        }

        [TestMethod]
        public void TillTile_FiresOnTileTilled()
        {
            Point? fired = null;
            _service.OnTileTilled += t => fired = t;

            _service.TillTile(new Point(3, 4));

            Assert.AreEqual(new Point(3, 4), fired);
        }

        [TestMethod]
        public void RestoreAllTilledTiles_RewritesGidsFromFlags()
        {
            _tileState.SetFlag(new Point(5, 5), TileStateFlag.Tilled);
            _tileState.SetFlag(new Point(6, 5), TileStateFlag.Tilled);
            _tileState.SetFlag(new Point(9, 9), TileStateFlag.ReadyToTill);   // planned only — no real tile

            _service.RestoreAllTilledTiles();

            Assert.AreEqual(GameConfig.TillZerothGid + 4, GidAt(5, 5));
            Assert.AreEqual(GameConfig.TillZerothGid + 2, GidAt(6, 5));
            Assert.AreEqual(0, GidAt(9, 9));
        }

        [TestMethod]
        public void TillTile_DiagonalNeighbor_DoesNotConnect()
        {
            _service.TillTile(new Point(5, 5));
            _service.TillTile(new Point(6, 6));

            Assert.AreEqual(GameConfig.TillZerothGid, GidAt(5, 5));
            Assert.AreEqual(GameConfig.TillZerothGid, GidAt(6, 6));
        }
    }
}
