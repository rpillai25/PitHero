using Nez.Tiled;
using PitHero.Rendering;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The Nez-facing shadow layers (issue #428): after Bind and a sync every non-empty cell of every
    /// copied layer has a drawable tile, including the cells that equal the live map (the first live
    /// check found Base and Detail missing because a cloned grid skipped exactly those cells).
    /// </summary>
    [TestClass]
    public class ShadowTileLayersBindTests
    {
        private const int ChunkTicks = 120;
        private const int W = 5, H = 3;
        private static readonly string[] Names = { "Base", "Detail", "FogOfWar" };

        private static TmxMap NewMap(uint[][] liveGrids)
        {
            var map = new TmxMap { Width = W, Height = H, TileWidth = 32, TileHeight = 32, Orientation = OrientationType.Orthogonal };
            map.Tilesets = new TmxList<TmxTileset>();
            map.Tilesets.Add(new TmxTileset { Map = map, Name = "tiles", FirstGid = 1, TileWidth = 32, TileHeight = 32 });
            map.Layers = new TmxList<ITmxLayer>();
            for (int i = 0; i < Names.Length; i++)
            {
                map.Layers.Add(new TmxLayer
                {
                    Map = map, Name = Names[i], Visible = true, Opacity = 1f, Width = W, Height = H,
                    Grid = liveGrids[i], Tiles = new System.Collections.Generic.Dictionary<uint, TmxLayerTile>(),
                });
            }
            return map;
        }

        private static FrameStore StoreWithKeyframe(uint[][] grids, params TileEvent[] events)
        {
            var store = new FrameStore(ChunkTicks, long.MaxValue);
            var builder = new FrameChunkBuilder(ChunkTicks);
            builder.Begin(0);
            for (int l = 0; l < grids.Length; l++)
                builder.SetTileKeyframeLayer((byte)l, W, H, grids[l]);
            int e = 0;
            for (int t = 0; t < ChunkTicks; t++)
            {
                builder.BeginTick(t);
                builder.EndTick(new HudRecord());
                while (e < events.Length && events[e].Tick == t)
                {
                    builder.AddTileEvent(events[e].Tick, events[e].Layer, events[e].X, events[e].Y, events[e].Gid);
                    e++;
                }
            }
            store.Add(FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null)));
            return store;
        }

        [TestMethod]
        public void EveryRecordedCellHasATileAfterTheFirstSync_EvenWhenItEqualsTheLiveMap()
        {
            var live = new[] { Fill(2), Fill(3), Fill(0) };
            var recorded = new[] { Fill(2), Fill(3), Fill(4) }; // Base and Detail identical to live; fog fully covered
            var map = NewMap(live);
            var layers = new ShadowTileLayers();
            layers.Bind(map);
            Assert.IsTrue(layers.IsBound);
            Assert.IsTrue(layers.SyncTo(StoreWithKeyframe(recorded), 10));

            for (int l = 0; l < 3; l++)
            {
                var copy = layers.GetLayer(l);
                Assert.IsNotNull(copy, Names[l]);
                Assert.AreNotSame(map.Layers[l], copy);
                for (int i = 0; i < W * H; i++)
                {
                    Assert.AreEqual(recorded[l][i], copy.Grid[i], Names[l] + " cell " + i);
                    var tile = copy.GetTile(i);
                    if (recorded[l][i] == 0)
                        Assert.IsNull(tile);
                    else
                        Assert.IsNotNull(tile, Names[l] + " cell " + i + " has no tile to draw");
                }
            }
            // the live layers are untouched
            Assert.AreEqual(0u, ((TmxLayer)map.Layers[2]).Grid[0]);
        }

        [TestMethod]
        public void EventsAfterTheKeyframeCreateTilesForNewGids()
        {
            var grids = new[] { Fill(1), Fill(0), Fill(0) };
            var map = NewMap(new[] { Fill(1), Fill(0), Fill(0) });
            var layers = new ShadowTileLayers();
            layers.Bind(map);
            var store = StoreWithKeyframe(grids, new TileEvent(20, 1, 2, 1, 7), new TileEvent(40, 0, 0, 0, 0));
            Assert.IsTrue(layers.SyncTo(store, 19));
            Assert.IsNull(layers.GetLayer(1).GetTile(2, 1));
            Assert.IsTrue(layers.SyncTo(store, 20));
            Assert.AreEqual(7, layers.GetLayer(1).GetTile(2, 1).Gid);
            Assert.IsTrue(layers.SyncTo(store, 40));
            Assert.IsNull(layers.GetLayer(0).GetTile(0, 0));
            Assert.IsNotNull(layers.GetLayer(0).GetTile(1, 0));
        }

        private static uint[] Fill(uint gid)
        {
            var g = new uint[W * H];
            for (int i = 0; i < g.Length; i++) g[i] = gid;
            return g;
        }
    }
}
