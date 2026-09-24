using System;
using System.Collections.Generic;
using System.IO;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The session sidecar worker: raw chunks handed over on the main thread come back compressed and
    /// on disk in order, the store gets them on Poll, truncation and reopen keep the file consistent,
    /// and the file serves as the store's reload source.
    /// </summary>
    [TestClass]
    public class FrameSessionSidecarTests
    {
        private const int ChunkTicks = 60;
        private static readonly FrameSidecarIdentity Identity = new FrameSidecarIdentity(77, 638100000000000000L, GameConfig.SimulationVersion, GameConfig.ReplayFrameFormatVersion, -1);

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_session_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static List<RawFrameChunk> RawChunks(FrameTestWorld world, int count, List<FrameTestWorld.TickRecord> records, SpriteKeyRegistry registry)
        {
            var builder = new FrameChunkBuilder(ChunkTicks);
            var raws = new List<RawFrameChunk>();
            for (int c = 0; c < count; c++)
            {
                for (int t = 0; t < ChunkTicks; t++)
                {
                    var rec = world.Capture();
                    records.Add(rec);
                    FrameTestWorld.Feed(builder, rec);
                }
                raws.Add(builder.Finish(registry));
            }
            return raws;
        }

        [TestMethod]
        public void Enqueue_Poll_DeliversChunksInOrder_AndSpillsThemToDisk()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "session_1.frames");
                var registry = new SpriteKeyRegistry();
                var sidecar = FrameSessionSidecar.OpenOrCreate(path, Identity, ChunkTicks, registry);
                Assert.IsNotNull(sidecar);
                Assert.IsFalse(sidecar.WasReopened);
                var store = new FrameStore(ChunkTicks, long.MaxValue);
                store.Preload(sidecar);
                store.IsSpilled = i => i < sidecar.SpilledChunkCount;

                var records = new List<FrameTestWorld.TickRecord>();
                var raws = RawChunks(FrameTestWorld.Small(31), 5, records, registry);
                for (int i = 0; i < raws.Count; i++)
                    sidecar.Enqueue(raws[i]);
                sidecar.Drain(store);
                Assert.AreEqual(0, sidecar.PendingCount);
                Assert.AreEqual(5, sidecar.SpilledChunkCount);
                Assert.AreEqual(5, sidecar.ChunkCount);
                Assert.AreEqual(5 * ChunkTicks - 1, sidecar.EndTick);
                Assert.AreEqual(5, store.ChunkCount);
                Assert.IsFalse(sidecar.IsFailed);
                Assert.IsTrue(sidecar.BytesOnDisk > FrameSidecarFile.HeaderSize);
                for (long tick = 0; tick < 5 * ChunkTicks; tick++)
                {
                    Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                    FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "polled");
                }

                // Evict everything from memory; every chunk comes back from the session file
                store.MemoryBudgetBytes = 1;
                store.Add(store.TryGetChunk(4, out var last) ? last : null);
                Assert.IsTrue(store.MemoryChunkCount <= 1);
                for (long tick = 0; tick < 5 * ChunkTicks; tick += 7)
                {
                    Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                    FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "reloaded");
                }

                sidecar.Finish(store, 5 * ChunkTicks);
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, FrameSidecarReader.Open(path, Identity.MasterSeed, Identity.RecordedAtUtcTicks, Identity.SimulationVersion, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter);
                    Assert.AreEqual(5, reader.ChunkCount);
                    Assert.AreEqual(5 * ChunkTicks, reader.TotalTicks);
                }
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void Reopen_ContinuesTheFile_RebuildsTheRegistry_AndTruncateKeepsAPrefix()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "session_2.frames");
                var registry = new SpriteKeyRegistry();
                registry.InternSprite(new SpriteKey("Atlases/Actors.png", 0, 0, 32, 32));
                registry.InternString("hello");
                var records = new List<FrameTestWorld.TickRecord>();
                var world = FrameTestWorld.Small(32);
                var first = FrameSessionSidecar.OpenOrCreate(path, Identity, ChunkTicks, registry);
                var store = new FrameStore(ChunkTicks, long.MaxValue);
                store.Preload(first);
                var raws = RawChunks(world, 3, records, registry);
                for (int i = 0; i < raws.Count; i++)
                    first.Enqueue(raws[i]);
                first.Drain(store);
                first.Dispose(); // no footer: a crash or a scene swap

                var rebuilt = new SpriteKeyRegistry();
                var second = FrameSessionSidecar.OpenOrCreate(path, Identity, ChunkTicks, rebuilt);
                Assert.IsNotNull(second);
                Assert.IsTrue(second.WasReopened);
                Assert.AreEqual(3, second.ChunkCount);
                Assert.AreEqual(3, second.SpilledChunkCount);
                Assert.AreEqual(1, rebuilt.SpriteCount);
                Assert.AreEqual("hello", rebuilt.GetString(1));
                Assert.AreEqual(3, rebuilt.FlushCount);

                var store2 = new FrameStore(ChunkTicks, long.MaxValue);
                store2.Preload(second);
                Assert.AreEqual(3 * ChunkTicks - 1, store2.EndTick);
                for (long tick = 0; tick < 3 * ChunkTicks; tick++)
                {
                    Assert.IsTrue(store2.TryGetFrame(tick, out var frame), "tick " + tick);
                    FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "reopened");
                }

                // Time Travel: keep one chunk on disk, then continue from chunk 1
                second.TruncateChunks(1);
                Assert.AreEqual(1, second.ChunkCount);
                Assert.AreEqual(1, second.SpilledChunkCount);
                Assert.IsFalse(second.TryLoadChunk(1, out _));
                store2.TruncateAfter(ChunkTicks - 1);
                var more = RawChunks(FrameTestWorld.Small(33), 2, new List<FrameTestWorld.TickRecord>(), rebuilt);
                // Re-number the new chunks to follow chunk 0: rebuild them on a builder that starts at tick 60
                var builder = new FrameChunkBuilder(ChunkTicks);
                builder.Begin(ChunkTicks);
                var world2 = FrameTestWorld.Small(34);
                var records2 = new List<FrameTestWorld.TickRecord>();
                for (int t = 0; t < ChunkTicks; t++)
                {
                    var rec = world2.Capture();
                    rec.Tick += ChunkTicks;
                    records2.Add(rec);
                    FrameTestWorld.Feed(builder, rec);
                }
                more[0].Release();
                more[1].Release();
                second.Enqueue(builder.Finish(rebuilt));
                second.Drain(store2);
                Assert.AreEqual(2, second.ChunkCount);
                Assert.AreEqual(2 * ChunkTicks - 1, store2.EndTick);
                Assert.IsTrue(store2.TryGetFrame(ChunkTicks + 5, out var f));
                FrameTestWorld.AssertFrameEquals(records2[5], f, "after truncate");

                // A different identity refuses the file and starts over
                second.Finish(store2, 2 * ChunkTicks);
                var other = new FrameSidecarIdentity(Identity.MasterSeed + 1, Identity.RecordedAtUtcTicks, Identity.SimulationVersion, Identity.FrameFormatVersion, -1);
                var third = FrameSessionSidecar.OpenOrCreate(path, other, ChunkTicks, new SpriteKeyRegistry());
                Assert.IsNotNull(third);
                Assert.IsFalse(third.WasReopened);
                Assert.AreEqual(0, third.ChunkCount);
                third.Dispose();
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
