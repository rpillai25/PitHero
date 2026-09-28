using System;
using System.Collections.Generic;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>Pins the in-memory chunk ring: random access, budget eviction only after a spill, truncation, preload.</summary>
    [TestClass]
    public class FrameStoreTests
    {
        private const int ChunkTicks = 60;

        /// <summary>A fake sidecar: chunks by index, counting loads.</summary>
        private sealed class MemorySource : IFrameChunkSource
        {
            public readonly Dictionary<int, FrameChunk> Chunks = new Dictionary<int, FrameChunk>();
            public int Loads;
            public int ChunkCount { get; set; }
            public long EndTick { get; set; } = -1;

            public void Add(FrameChunk chunk)
            {
                int index = (int)(chunk.FirstTick / ChunkTicks);
                Chunks[index] = chunk;
                if (index + 1 > ChunkCount) ChunkCount = index + 1;
                if (chunk.LastTick > EndTick) EndTick = chunk.LastTick;
            }

            public bool TryLoadChunk(int chunkIndex, out FrameChunk chunk)
            {
                Loads++;
                return Chunks.TryGetValue(chunkIndex, out chunk);
            }
        }

        private static List<FrameChunk> Encode(int seed, int chunkCount, List<FrameTestWorld.TickRecord> records)
            => FrameTestWorld.EncodeChunks(FrameTestWorld.Small(seed), ChunkTicks, chunkCount, records);

        [TestMethod]
        public void TryGetFrame_RandomAccess_MatchesReference()
        {
            var records = new List<FrameTestWorld.TickRecord>();
            var chunks = Encode(11, 5, records);
            var store = new FrameStore(ChunkTicks, long.MaxValue);
            Assert.IsTrue(store.IsEmpty);
            Assert.IsFalse(store.TryGetFrame(0, out _));
            for (int i = 0; i < chunks.Count; i++)
                store.Add(chunks[i]);
            Assert.AreEqual(5, store.ChunkCount);
            Assert.AreEqual(5 * ChunkTicks - 1, store.EndTick);
            Assert.AreEqual(5, store.MemoryChunkCount);

            var rng = new Random(3);
            for (int n = 0; n < 200; n++)
            {
                long tick = rng.Next(5 * ChunkTicks);
                Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "random");
            }
            for (long tick = 0; tick < 5 * ChunkTicks; tick++)
            {
                Assert.IsTrue(store.TryGetFrame(tick, out var frame));
                FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "sequential");
            }
            Assert.IsFalse(store.TryGetFrame(-1, out _));
            Assert.IsFalse(store.TryGetFrame(5 * ChunkTicks, out _));
            Assert.IsTrue(store.TryGetDecodedChunk(2, out var decoded));
            Assert.AreEqual(2 * ChunkTicks, decoded.FirstTick);
            Assert.IsFalse(store.TryGetDecodedChunk(7, out _));
        }

        [TestMethod]
        public void Budget_EvictsOnlySpilledChunks_LeastRecentlyUsedFirst_AndReloadsFromSource()
        {
            var records = new List<FrameTestWorld.TickRecord>();
            var chunks = Encode(12, 6, records);
            long budget = chunks[0].Length + chunks[1].Length + chunks[2].Length + chunks[3].Length / 2; // three and a half chunks

            // No source: nothing is ever evicted, even over budget
            var lonely = new FrameStore(ChunkTicks, 1);
            lonely.IsSpilled = _ => true;
            for (int i = 0; i < chunks.Count; i++)
                lonely.Add(chunks[i]);
            Assert.AreEqual(6, lonely.MemoryChunkCount, "no source to reload from: never evict");

            var source = new MemorySource();
            var store = new FrameStore(ChunkTicks, budget);
            var spilled = new HashSet<int>();
            store.IsSpilled = index => spilled.Contains(index);
            store.Preload(source);
            for (int i = 0; i < chunks.Count; i++)
                store.Add(chunks[i]);
            Assert.AreEqual(6, store.MemoryChunkCount, "nothing spilled yet: over budget but nothing evictable");
            Assert.IsTrue(store.MemoryBytes > store.MemoryBudgetBytes);

            // Spill 0..3 (the recorder wrote them); the store may now evict them. Touch chunk 0 so it is the most recent.
            for (int i = 0; i < 4; i++)
            {
                spilled.Add(i);
                source.Add(chunks[i]);
            }
            Assert.IsTrue(store.TryGetFrame(5, out _), "touch chunk 0 so it is the most recently used spilled chunk");
            store.Add(chunks[5]); // any add re-enforces the budget: 6 chunks > 3.5, evict LRU spilled until under
            Assert.IsTrue(store.IsInMemory(4) && store.IsInMemory(5), "unspilled chunks are pinned by the budget rule");
            Assert.IsTrue(store.IsInMemory(0), "most recently used spilled chunk survives");
            Assert.IsFalse(store.IsInMemory(1) || store.IsInMemory(2) || store.IsInMemory(3), "LRU spilled chunks evicted");
            Assert.AreEqual(3, store.MemoryChunkCount);
            Assert.IsTrue(store.MemoryBytes <= store.MemoryBudgetBytes);

            // Evicted chunks come back from the source and still decode correctly
            int loadsBefore = source.Loads;
            Assert.IsTrue(store.TryGetFrame(2 * ChunkTicks + 7, out var frame));
            FrameTestWorld.AssertFrameEquals(records[2 * ChunkTicks + 7], frame, "reloaded");
            Assert.AreEqual(loadsBefore + 1, source.Loads);
            Assert.IsTrue(store.IsInMemory(2));
            Assert.IsTrue(store.TryGetFrame(2 * ChunkTicks + 8, out frame));
            Assert.AreEqual(loadsBefore + 1, source.Loads, "second read of the same chunk is served from memory");
            for (long tick = 0; tick < 6 * ChunkTicks; tick++)
            {
                Assert.IsTrue(store.TryGetFrame(tick, out frame), "tick " + tick);
                FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "sweep");
            }
        }

        [TestMethod]
        public void TruncateAfter_DropsLaterFrames_ShortensTheCutChunk_AndPinsIt()
        {
            var records = new List<FrameTestWorld.TickRecord>();
            var chunks = Encode(13, 4, records);
            var source = new MemorySource();
            for (int i = 0; i < chunks.Count; i++)
                source.Add(chunks[i]);
            var store = new FrameStore(ChunkTicks, 1);
            store.Preload(source);
            Assert.AreEqual(4, store.ChunkCount);
            Assert.AreEqual(4 * ChunkTicks - 1, store.EndTick);

            long cut = 2 * ChunkTicks + 10;
            store.TruncateAfter(cut);
            Assert.AreEqual(cut, store.EndTick);
            Assert.AreEqual(3, store.ChunkCount);
            Assert.IsFalse(store.TryGetFrame(cut + 1, out _));
            Assert.IsFalse(store.TryGetChunk(3, out _), "the source's later chunk is never served again");
            Assert.IsTrue(store.TryGetChunk(2, out var cutChunk));
            Assert.AreEqual(11, cutChunk.TickCount);
            Assert.IsTrue(store.IsInMemory(2), "cut chunk pinned in memory despite a 1-byte budget");
            for (long tick = 0; tick <= cut; tick++)
            {
                Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "after cut");
            }
            Assert.IsTrue(store.IsInMemory(2), "still pinned after reads evicted the others");

            // Recording continues: the recorder replaces the cut chunk and adds the next one
            var again = FrameChunkCodec.Truncate(chunks[2], cut + 5);
            store.Add(again);
            Assert.AreEqual(cut + 5, store.EndTick);
            store.TruncateAfter(-1);
            Assert.IsTrue(store.IsEmpty);
            Assert.AreEqual(0, store.ChunkCount);
            Assert.IsFalse(store.TryGetFrame(0, out _));

            // A cut on a chunk boundary keeps the whole earlier chunk and no partial one
            var store2 = new FrameStore(ChunkTicks, long.MaxValue);
            for (int i = 0; i < chunks.Count; i++)
                store2.Add(chunks[i]);
            store2.TruncateAfter(2 * ChunkTicks - 1);
            Assert.AreEqual(2, store2.ChunkCount);
            Assert.AreEqual(2 * ChunkTicks - 1, store2.EndTick);
            Assert.IsTrue(store2.TryGetChunk(1, out var whole) && whole.TickCount == ChunkTicks);
            store2.TruncateAfter(10_000);
            Assert.AreEqual(2, store2.ChunkCount, "cut beyond the end changes nothing");
        }

        [TestMethod]
        public void Preload_FromStore_MovesChunksAndSource()
        {
            var records = new List<FrameTestWorld.TickRecord>();
            var chunks = Encode(14, 3, records);
            var source = new MemorySource();
            source.Add(chunks[0]);
            var old = new FrameStore(ChunkTicks, long.MaxValue);
            old.Preload(source);
            old.Add(chunks[1]);
            old.Add(chunks[2]);

            var fresh = new FrameStore(ChunkTicks, long.MaxValue);
            fresh.Preload(old);
            Assert.AreEqual(0, old.MemoryChunkCount);
            Assert.IsTrue(old.IsEmpty);
            Assert.AreEqual(3, fresh.ChunkCount);
            Assert.AreEqual(3 * ChunkTicks - 1, fresh.EndTick);
            Assert.AreSame(source, fresh.Source);
            for (long tick = 0; tick < 3 * ChunkTicks; tick++)
            {
                Assert.IsTrue(fresh.TryGetFrame(tick, out var frame), "tick " + tick);
                FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "preloaded");
            }
            Assert.AreEqual(1, source.Loads, "chunk 0 came from the source, 1 and 2 were moved");
            Assert.ThrowsException<ArgumentException>(() => new FrameStore(ChunkTicks + 1, 1).Preload(fresh));
        }

        [TestMethod]
        public void Add_RejectsMisalignedOrOversizedChunks_AndReplacesExisting()
        {
            var records = new List<FrameTestWorld.TickRecord>();
            var chunks = Encode(15, 2, records);
            var store = new FrameStore(ChunkTicks, long.MaxValue);
            Assert.ThrowsException<ArgumentNullException>(() => store.Add(null));
            var shifted = FrameChunkCodec.Truncate(chunks[1], ChunkTicks + 5);
            store.Add(shifted);
            Assert.AreEqual(ChunkTicks + 5, store.EndTick);
            store.Add(chunks[1]);
            Assert.AreEqual(2 * ChunkTicks - 1, store.EndTick, "replacing a chunk updates the end tick");
            Assert.AreEqual(1, store.MemoryChunkCount);
            store.Add(chunks[0]);
            Assert.AreEqual(2, store.MemoryChunkCount);
            Assert.AreEqual(2 * ChunkTicks - 1, store.EndTick, "adding an earlier chunk keeps the end tick");

            // A chunk built with a different chunk size does not start on this store's boundary
            var other = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(1), 50, 2, new List<FrameTestWorld.TickRecord>());
            Assert.ThrowsException<ArgumentException>(() => store.Add(other[1]));
            var big = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(1), ChunkTicks * 2, 1, new List<FrameTestWorld.TickRecord>());
            Assert.ThrowsException<ArgumentException>(() => store.Add(big[0]));
        }
    }
}
