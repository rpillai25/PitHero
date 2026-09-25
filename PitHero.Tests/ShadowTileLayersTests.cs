using System;
using System.Collections.Generic;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The viewer's shadow tile grid (issue #428): a tile keyframe plus the recorded tile events
    /// reproduce the mutable layers at any tick, in any seek order, across chunk boundaries and for
    /// chunks without their own keyframe.
    /// </summary>
    [TestClass]
    public class ShadowTileLayersTests
    {
        private const int ChunkTicks = 120;
        private const int W = 6, H = 4;

        private sealed class Reference
        {
            public readonly uint[][] Layers = { new uint[W * H], new uint[W * H], new uint[W * H] };
            public readonly List<TileEvent> Events = new List<TileEvent>();
            private readonly Dictionary<long, uint[][]> _snapshots = new Dictionary<long, uint[][]>();

            public void Write(long tick, byte layer, int x, int y, int gid)
            {
                Events.Add(new TileEvent(tick, layer, (ushort)x, (ushort)y, gid));
            }

            /// <summary>The three layers as they were right after every event at or before <paramref name="tick"/>.</summary>
            public uint[][] At(long tick)
            {
                if (_snapshots.TryGetValue(tick, out var cached))
                    return cached;
                var result = new[] { new uint[W * H], new uint[W * H], new uint[W * H] };
                for (int l = 0; l < 3; l++)
                    Array.Copy(Layers[l], result[l], W * H);
                for (int i = 0; i < Events.Count; i++)
                {
                    var e = Events[i];
                    if (e.Tick > tick)
                        break;
                    result[e.Layer][e.X + e.Y * W] = (uint)e.Gid;
                }
                _snapshots[tick] = result;
                return result;
            }
        }

        /// <summary>Encodes <paramref name="chunkCount"/> chunks from the reference; keyframes only in the chunks listed.</summary>
        private static FrameStore Build(Reference reference, int chunkCount, HashSet<int> keyframeChunks)
        {
            var store = new FrameStore(ChunkTicks, long.MaxValue);
            var builder = new FrameChunkBuilder(ChunkTicks);
            int eventIndex = 0;
            for (int c = 0; c < chunkCount; c++)
            {
                long first = (long)c * ChunkTicks;
                builder.Begin(first);
                if (keyframeChunks.Contains(c))
                {
                    var state = reference.At(first - 1);
                    for (int l = 0; l < 3; l++)
                        builder.SetTileKeyframeLayer((byte)l, W, H, state[l]);
                }
                for (int t = 0; t < ChunkTicks; t++)
                {
                    long tick = first + t;
                    builder.BeginTick(tick);
                    builder.EndTick(new HudRecord { Gold = tick });
                    while (eventIndex < reference.Events.Count && reference.Events[eventIndex].Tick == tick)
                    {
                        var e = reference.Events[eventIndex++];
                        builder.AddTileEvent(e.Tick, e.Layer, e.X, e.Y, e.Gid);
                    }
                }
                store.Add(FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null)));
            }
            return store;
        }

        private static Reference MakeReference(int seed, int chunkCount)
        {
            var rng = new Random(seed);
            var reference = new Reference();
            for (int l = 0; l < 3; l++)
                for (int i = 0; i < W * H; i++)
                    reference.Layers[l][i] = (uint)rng.Next(0, 5);
            long lastTick = 0;
            while (lastTick < chunkCount * ChunkTicks - 1)
            {
                lastTick += rng.Next(0, 7);
                if (lastTick >= chunkCount * ChunkTicks)
                    break;
                reference.Write(lastTick, (byte)rng.Next(0, 3), rng.Next(0, W), rng.Next(0, H), rng.Next(0, 9));
            }
            return reference;
        }

        private static void AssertMatches(ShadowTileGrid grid, Reference reference, long tick)
        {
            var expected = reference.At(tick);
            for (int l = 0; l < 3; l++)
            {
                Assert.AreEqual(W, grid.Width(l));
                Assert.AreEqual(H, grid.Height(l));
                CollectionAssert.AreEqual(expected[l], grid.Gids(l), "layer " + l + " at tick " + tick);
            }
        }

        [TestMethod]
        public void SequentialTicks_ApplyEventsIncrementally()
        {
            var reference = MakeReference(1, 3);
            var store = Build(reference, 3, new HashSet<int> { 0, 1, 2 });
            var grid = new ShadowTileGrid();
            for (long tick = 0; tick < 3 * ChunkTicks; tick++)
            {
                Assert.IsTrue(grid.SyncTo(store, tick));
                AssertMatches(grid, reference, tick);
            }
        }

        [TestMethod]
        public void ArbitrarySeeks_RebuildFromTheNearestKeyframe()
        {
            var reference = MakeReference(2, 4);
            var store = Build(reference, 4, new HashSet<int> { 0, 1, 2, 3 });
            var grid = new ShadowTileGrid();
            long[] order = { 300, 5, 479, 121, 120, 119, 240, 0, 361, 60 };
            for (int i = 0; i < order.Length; i++)
            {
                Assert.IsTrue(grid.SyncTo(store, order[i]));
                AssertMatches(grid, reference, order[i]);
            }
        }

        [TestMethod]
        public void ChunksWithoutKeyframes_WalkBackToTheLastOne()
        {
            var reference = MakeReference(3, 4);
            var store = Build(reference, 4, new HashSet<int> { 0, 2 }); // chunks 1 and 3 carry no keyframe
            var grid = new ShadowTileGrid();
            long[] order = { 200, 400, 130, 479, 250 };
            for (int i = 0; i < order.Length; i++)
            {
                Assert.IsTrue(grid.SyncTo(store, order[i]), "tick " + order[i]);
                AssertMatches(grid, reference, order[i]);
            }
        }

        [TestMethod]
        public void VersionChangesOnlyWhenAGidChanges()
        {
            var reference = new Reference();
            reference.Write(10, 0, 1, 1, 7);
            reference.Write(50, 0, 1, 1, 7); // same gid again
            var store = Build(reference, 1, new HashSet<int> { 0 });
            var grid = new ShadowTileGrid();
            Assert.IsTrue(grid.SyncTo(store, 0));
            int v = grid.Version;
            Assert.IsTrue(grid.SyncTo(store, 9));
            Assert.AreEqual(v, grid.Version);
            Assert.IsTrue(grid.SyncTo(store, 10));
            Assert.AreEqual(v + 1, grid.Version);
            Assert.IsTrue(grid.SyncTo(store, 60));
            Assert.AreEqual(v + 1, grid.Version, "a same-gid event is not a change");
        }

        [TestMethod]
        public void OutOfRangeTicks_AreRejected()
        {
            var reference = MakeReference(4, 1);
            var store = Build(reference, 1, new HashSet<int> { 0 });
            var grid = new ShadowTileGrid();
            Assert.IsFalse(grid.SyncTo(store, -1));
            Assert.IsFalse(grid.SyncTo(store, ChunkTicks));
            Assert.IsFalse(grid.SyncTo(null, 0));
        }
    }
}
