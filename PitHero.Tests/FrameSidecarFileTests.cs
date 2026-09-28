using System;
using System.Collections.Generic;
using System.IO;
using PitHero.Services;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// Pins the .frames sidecar: append + footer + reopen; a footer-less (crashed) file rebuilds its index
    /// and loses only the partial tail; identity and format mismatches are rejected; reopen-for-append;
    /// and a store backed by a sidecar reader evicts and reloads.
    /// </summary>
    [TestClass]
    public class FrameSidecarFileTests
    {
        private const int ChunkTicks = 60;
        private static readonly FrameSidecarIdentity Identity = new FrameSidecarIdentity(123456, 638000000000000000L, GameConfig.SimulationVersion, GameConfig.ReplayFrameFormatVersion, -1);

        private static string NewTempFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_frames_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "session" + GameConfig.ReplayFrameFileExtension);
        }

        private static void Cleanup(string path)
        {
            try { Directory.Delete(Path.GetDirectoryName(path), recursive: true); } catch (IOException) { }
        }

        private static FrameSidecarFile.OpenResult OpenMatching(string path, out FrameSidecarReader reader)
            => FrameSidecarReader.Open(path, Identity.MasterSeed, Identity.RecordedAtUtcTicks, Identity.SimulationVersion, out reader);

        [TestMethod]
        public void Append_Footer_Reopen_ServesEveryChunk_AndRebuildsTheRegistry()
        {
            string path = NewTempFile();
            try
            {
                var registry = new SpriteKeyRegistry();
                registry.InternSprite(new SpriteKey("Atlases/Actors.png", 0, 0, 32, 32));
                registry.InternString("first");
                var records = new List<FrameTestWorld.TickRecord>();
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(21), ChunkTicks, 4, records, registry);
                registry.InternSprite(new SpriteKey("Atlases/Actors.png", 32, 0, 32, 32)); // pending, never flushed: not in the file

                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    for (int i = 0; i < chunks.Count; i++)
                        writer.Append(chunks[i]);
                    Assert.AreEqual(4, writer.ChunkCount);
                    Assert.AreEqual(4 * ChunkTicks - 1, writer.EndTick);
                    writer.Finish(4 * ChunkTicks);
                    Assert.IsTrue(writer.IsFinished);
                    Assert.IsFalse(writer.IsOpen);
                    Assert.ThrowsException<InvalidOperationException>(() => writer.Append(chunks[0]));
                }

                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(path, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter);
                    Assert.AreEqual(4, reader.ChunkCount);
                    Assert.AreEqual(4 * ChunkTicks, reader.TotalTicks);
                    Assert.AreEqual(4 * ChunkTicks - 1, reader.EndTick);
                    Assert.AreEqual(ChunkTicks, reader.ChunkTicks);
                    Assert.AreEqual(Identity.MasterSeed, reader.Identity.MasterSeed);
                    Assert.IsFalse(reader.TryLoadChunk(4, out _));
                    Assert.IsFalse(reader.TryLoadChunk(-1, out _));
                    for (int i = 0; i < 4; i++)
                    {
                        Assert.IsTrue(reader.TryLoadChunk(i, out var chunk), "chunk " + i);
                        Assert.AreEqual(chunks[i].FirstTick, chunk.FirstTick);
                        Assert.IsTrue(new ReadOnlySpan<byte>(chunks[i].Bytes).SequenceEqual(chunk.Bytes), "bytes of chunk " + i);
                    }
                    var rebuilt = new SpriteKeyRegistry();
                    reader.RebuildRegistry(rebuilt);
                    Assert.AreEqual(1, rebuilt.SpriteCount);
                    Assert.AreEqual(1, rebuilt.StringCount);
                    Assert.AreEqual(4, rebuilt.FlushCount);
                    Assert.AreEqual("first", rebuilt.GetString(1));

                    var store = new FrameStore(ChunkTicks, long.MaxValue);
                    store.Preload(reader);
                    Assert.AreEqual(4 * ChunkTicks - 1, store.EndTick);
                    for (long tick = 0; tick < 4 * ChunkTicks; tick++)
                    {
                        Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                        FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "from file");
                    }
                }
            }
            finally
            {
                Cleanup(path);
            }
        }

        [TestMethod]
        public void FooterlessFile_RebuildsIndexByScanning_LosesOnlyThePartialTail()
        {
            string path = NewTempFile();
            try
            {
                var records = new List<FrameTestWorld.TickRecord>();
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(22), ChunkTicks, 4, records);
                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    for (int i = 0; i < chunks.Count; i++)
                        writer.Append(chunks[i]);
                    // crash: no Finish
                }
                // Cut the last chunk in half, as a crash mid-write would
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                    fs.SetLength(fs.Length - chunks[3].Length / 2);

                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(path, out var reader));
                using (reader)
                {
                    Assert.IsFalse(reader.HasFooter);
                    Assert.AreEqual(3, reader.ChunkCount, "the partial fourth chunk is dropped");
                    Assert.AreEqual(3 * ChunkTicks, reader.TotalTicks);
                    Assert.AreEqual(3 * ChunkTicks - 1, reader.EndTick);
                    var store = new FrameStore(ChunkTicks, long.MaxValue);
                    store.Preload(reader);
                    for (long tick = 0; tick < 3 * ChunkTicks; tick++)
                    {
                        Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                        FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "scanned");
                    }
                }

                // An empty (header-only) unfinished file opens with no chunks
                string empty = Path.Combine(Path.GetDirectoryName(path), "empty.frames");
                FrameSidecarWriter.Create(empty, Identity, ChunkTicks).Dispose();
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(empty, out reader));
                using (reader)
                {
                    Assert.AreEqual(0, reader.ChunkCount);
                    Assert.AreEqual(-1, reader.EndTick);
                    Assert.AreEqual(0, reader.TotalTicks);
                }
            }
            finally
            {
                Cleanup(path);
            }
        }

        [TestMethod]
        public void Mismatches_AreRejected()
        {
            string path = NewTempFile();
            try
            {
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(23), ChunkTicks, 1, new List<FrameTestWorld.TickRecord>());
                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    writer.Append(chunks[0]);
                    writer.Finish(ChunkTicks);
                }

                Assert.AreEqual(FrameSidecarFile.OpenResult.Missing, FrameSidecarReader.Open(path + ".nope", out var r0));
                Assert.IsNull(r0);
                Assert.AreEqual(FrameSidecarFile.OpenResult.IdentityMismatch,
                    FrameSidecarReader.Open(path, Identity.MasterSeed + 1, Identity.RecordedAtUtcTicks, Identity.SimulationVersion, out var r1));
                Assert.IsNull(r1);
                Assert.AreEqual(FrameSidecarFile.OpenResult.IdentityMismatch,
                    FrameSidecarReader.Open(path, Identity.MasterSeed, Identity.RecordedAtUtcTicks + 1, Identity.SimulationVersion, out _));
                Assert.AreEqual(FrameSidecarFile.OpenResult.IdentityMismatch,
                    FrameSidecarReader.Open(path, Identity.MasterSeed, Identity.RecordedAtUtcTicks, Identity.SimulationVersion + 1, out _));

                // Older frame format version in the header
                var bytes = File.ReadAllBytes(path);
                bytes[4] = (byte)(GameConfig.ReplayFrameFormatVersion + 1);
                File.WriteAllBytes(path, bytes);
                Assert.AreEqual(FrameSidecarFile.OpenResult.FormatMismatch, OpenMatching(path, out _));

                // Bad magic
                bytes[4] = (byte)GameConfig.ReplayFrameFormatVersion;
                bytes[0] ^= 0xFF;
                File.WriteAllBytes(path, bytes);
                Assert.AreEqual(FrameSidecarFile.OpenResult.Corrupt, OpenMatching(path, out _));

                // Too short to hold a header
                File.WriteAllBytes(path, new byte[5]);
                Assert.AreEqual(FrameSidecarFile.OpenResult.Corrupt, OpenMatching(path, out _));
            }
            finally
            {
                Cleanup(path);
            }
        }

        [TestMethod]
        public void DamagedFooter_FallsBackToScanning()
        {
            string path = NewTempFile();
            try
            {
                var records = new List<FrameTestWorld.TickRecord>();
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(24), ChunkTicks, 3, records);
                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    for (int i = 0; i < chunks.Count; i++)
                        writer.Append(chunks[i]);
                    writer.Finish(3 * ChunkTicks);
                }
                var bytes = File.ReadAllBytes(path);
                bytes[bytes.Length - 1] ^= 0xFF; // end magic broken
                File.WriteAllBytes(path, bytes);

                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(path, out var reader));
                using (reader)
                {
                    Assert.IsFalse(reader.HasFooter);
                    Assert.AreEqual(3, reader.ChunkCount, "scan stops cleanly at the footer bytes");
                    Assert.IsTrue(reader.TryLoadChunk(2, out var chunk));
                    Assert.AreEqual(2 * ChunkTicks, chunk.FirstTick);
                }
            }
            finally
            {
                Cleanup(path);
            }
        }

        [TestMethod]
        public void Reopen_ContinuesAfterTheLastChunk_AndReplacesAPartialLastChunk()
        {
            string path = NewTempFile();
            try
            {
                var records = new List<FrameTestWorld.TickRecord>();
                var world = FrameTestWorld.Small(25);
                var chunks = FrameTestWorld.EncodeChunks(world, ChunkTicks, 3, records);
                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    writer.Append(chunks[0]);
                    writer.Append(chunks[1]);
                    writer.Finish(2 * ChunkTicks);
                }
                long finishedLength = new FileInfo(path).Length;

                // A partial third chunk (a save mid-chunk), then the full one replaces it
                var partial = FrameChunkCodec.Truncate(chunks[2], 2 * ChunkTicks + 9);
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, FrameSidecarWriter.Reopen(path, out var reopened));
                using (reopened)
                {
                    Assert.AreEqual(2, reopened.ChunkCount);
                    Assert.IsTrue(new FileInfo(path).Length < finishedLength, "footer cut off");
                    Assert.ThrowsException<ArgumentException>(() => reopened.Append(chunks[0]), "not contiguous");
                    reopened.Append(partial);
                    Assert.AreEqual(3, reopened.ChunkCount);
                    Assert.AreEqual(2 * ChunkTicks + 9, reopened.EndTick);
                    reopened.Append(chunks[2]);
                    Assert.AreEqual(3, reopened.ChunkCount, "same first tick replaces the last chunk");
                    Assert.AreEqual(3 * ChunkTicks - 1, reopened.EndTick);
                    reopened.Finish(3 * ChunkTicks);
                }

                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(path, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter);
                    Assert.AreEqual(3, reader.ChunkCount);
                    Assert.AreEqual(3 * ChunkTicks, reader.TotalTicks);
                    Assert.IsTrue(reader.TryLoadChunk(2, out var chunk));
                    Assert.AreEqual(ChunkTicks, chunk.TickCount);
                    var store = new FrameStore(ChunkTicks, long.MaxValue);
                    store.Preload(reader);
                    for (long tick = 0; tick < 3 * ChunkTicks; tick++)
                    {
                        Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                        FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "reopened");
                    }
                }
                Assert.AreEqual(FrameSidecarFile.OpenResult.Missing, FrameSidecarWriter.Reopen(path + ".nope", out _));
            }
            finally
            {
                Cleanup(path);
            }
        }

        private static RecordedConsoleLog SampleConsoleLog()
        {
            var log = new RecordedConsoleLog();
            log.Add(3, new[] { new ConsoleSegment("hello", Microsoft.Xna.Framework.Color.White) });
            log.Add(70, new[]
            {
                new ConsoleSegment("found ", Microsoft.Xna.Framework.Color.White),
                new ConsoleSegment("Rusty Blade", Microsoft.Xna.Framework.Color.Cyan, "RustyBlade"),
            });
            return log;
        }

        private static void AssertSampleConsoleLog(RecordedConsoleLog log)
        {
            Assert.AreEqual(2, log.Count);
            Assert.AreEqual(3, log[0].Tick);
            Assert.AreEqual(1, log[0].Segments.Length);
            Assert.AreEqual("hello", log[0].Segments[0].Text);
            Assert.IsNull(log[0].Segments[0].ItemName);
            Assert.AreEqual(70, log[1].Tick);
            Assert.AreEqual(2, log[1].Segments.Length);
            Assert.AreEqual("Rusty Blade", log[1].Segments[1].Text);
            Assert.AreEqual(Microsoft.Xna.Framework.Color.Cyan.PackedValue, log[1].Segments[1].Color.PackedValue);
            Assert.AreEqual("RustyBlade", log[1].Segments[1].ItemName);
        }

        /// <summary>
        /// Save Session Replay in the middle of a session (issue #429): the session file is copied as a
        /// finished sidecar (footer, console lines, only the chunks below the saved tick count) while the
        /// original stays open and keeps taking chunks.
        /// </summary>
        [TestMethod]
        public void ExportTo_WritesAFinishedCopy_WhileTheSourceKeepsAppending()
        {
            string path = NewTempFile();
            try
            {
                string dest = Path.Combine(Path.GetDirectoryName(path), "replay_Hero_1.frames");
                var records = new List<FrameTestWorld.TickRecord>();
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(27), ChunkTicks, 4, records);
                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    writer.Append(chunks[0]);
                    writer.Append(chunks[1]);
                    writer.Append(chunks[2]);
                    writer.ExportTo(dest, 3 * ChunkTicks, SampleConsoleLog());
                    Assert.IsTrue(writer.IsOpen, "the session file stays open");
                    writer.Append(chunks[3]);
                    Assert.AreEqual(4, writer.ChunkCount);

                    // A second export with a smaller tick count keeps only the chunks below it
                    string dest2 = Path.Combine(Path.GetDirectoryName(path), "replay_Hero_2.frames");
                    writer.ExportTo(dest2, 2 * ChunkTicks + 1, null);
                    Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(dest2, out var partial));
                    using (partial)
                    {
                        Assert.AreEqual(3, partial.ChunkCount, "chunk 2 starts below the tick count, chunk 3 does not");
                        Assert.AreEqual(2 * ChunkTicks + 1, partial.TotalTicks);
                        Assert.IsFalse(partial.HasConsoleLog);
                        partial.ReadConsoleLog(new RecordedConsoleLog()); // no section: nothing added, no throw
                    }
                }

                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(dest, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter);
                    Assert.AreEqual(3, reader.ChunkCount);
                    Assert.AreEqual(3 * ChunkTicks, reader.TotalTicks);
                    Assert.IsTrue(reader.HasConsoleLog);
                    var log = new RecordedConsoleLog();
                    reader.ReadConsoleLog(log);
                    AssertSampleConsoleLog(log);
                    var store = new FrameStore(ChunkTicks, long.MaxValue);
                    store.Preload(reader);
                    for (long tick = 0; tick < 3 * ChunkTicks; tick++)
                    {
                        Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                        FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "exported");
                    }
                }
                // The copy is a cache of THIS recording only
                Assert.AreEqual(FrameSidecarFile.OpenResult.IdentityMismatch,
                    FrameSidecarReader.Open(dest, Identity.MasterSeed, Identity.RecordedAtUtcTicks + 1, Identity.SimulationVersion, out _));

                // The source, closed without a footer, still scans to its four chunks
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(path, out var source));
                using (source)
                {
                    Assert.IsFalse(source.HasFooter);
                    Assert.AreEqual(4, source.ChunkCount);
                }
            }
            finally
            {
                Cleanup(path);
            }
        }

        /// <summary>The quit-time save: the session file is finished with its console lines and renamed to the replay's cache.</summary>
        [TestMethod]
        public void FinishAndMove_RenamesTheFinishedFile()
        {
            string path = NewTempFile();
            try
            {
                string dest = Path.Combine(Path.GetDirectoryName(path), "replay_Hero_3.frames");
                File.WriteAllBytes(dest, new byte[] { 1, 2, 3 }); // an older file at the destination is replaced
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(28), ChunkTicks, 2, new List<FrameTestWorld.TickRecord>());
                var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks);
                writer.Append(chunks[0]);
                writer.Append(chunks[1]);
                writer.FinishAndMove(dest, 2 * ChunkTicks, SampleConsoleLog());
                Assert.IsTrue(writer.IsFinished);
                Assert.IsFalse(writer.IsOpen);
                Assert.IsFalse(File.Exists(path), "moved away");

                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(dest, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter);
                    Assert.AreEqual(2, reader.ChunkCount);
                    Assert.AreEqual(2 * ChunkTicks, reader.TotalTicks);
                    var log = new RecordedConsoleLog();
                    reader.ReadConsoleLog(log);
                    AssertSampleConsoleLog(log);
                }
            }
            finally
            {
                Cleanup(path);
            }
        }

        /// <summary>
        /// Reopening a finished file for appending (the way back to a live session) gives the console
        /// lines of its footer back, and a footer written before the console section existed still reads.
        /// </summary>
        [TestMethod]
        public void Reopen_RecoversTheConsoleLog_AndAFooterWithoutOneStillReads()
        {
            string path = NewTempFile();
            try
            {
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(29), ChunkTicks, 2, new List<FrameTestWorld.TickRecord>());
                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    writer.Append(chunks[0]);
                    writer.Append(chunks[1]);
                    writer.Finish(2 * ChunkTicks, SampleConsoleLog());
                }
                var log = new RecordedConsoleLog();
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, FrameSidecarWriter.Reopen(path, new SpriteKeyRegistry(), log, out var reopened));
                using (reopened)
                {
                    Assert.AreEqual(2, reopened.ChunkCount);
                    AssertSampleConsoleLog(log);
                }

                // Pre-#429 layout: footer = magic, totalTicks, count, entries, then the trailer at once
                using (var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks))
                {
                    writer.Append(chunks[0]);
                    writer.Finish(ChunkTicks, null);
                }
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(path, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter);
                    Assert.IsFalse(reader.HasConsoleLog);
                    Assert.AreEqual(1, reader.ChunkCount);
                    Assert.AreEqual(ChunkTicks, reader.TotalTicks);
                }
            }
            finally
            {
                Cleanup(path);
            }
        }

        [TestMethod]
        public void Store_BackedBySidecar_EvictsUnderBudget_AndReloadsFromDisk()
        {
            string path = NewTempFile();
            try
            {
                var records = new List<FrameTestWorld.TickRecord>();
                var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(26), ChunkTicks, 6, records);
                var writer = FrameSidecarWriter.Create(path, Identity, ChunkTicks);
                var spilled = new HashSet<int>();
                var store = new FrameStore(ChunkTicks, chunks[0].Length * 2L);
                store.IsSpilled = i => spilled.Contains(i);

                // Live session: the recorder adds to the store, the worker appends to the file, the reader is the store's source
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, FrameSidecarReader.Open(path, out var reader));
                using (reader)
                using (writer)
                {
                    store.Preload(reader);
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        store.Add(chunks[i]);
                        writer.Append(chunks[i]);
                        spilled.Add(i);
                    }
                    Assert.IsTrue(store.MemoryChunkCount < 6, "budget of ~2 chunks enforced once chunks are spilled");
                    // The reader opened before the chunks were written knows none of them; a fresh reader sees them
                    Assert.IsFalse(store.TryGetFrame(0, out _) && store.IsInMemory(0) == false && reader.ChunkCount > 0);
                }

                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, OpenMatching(path, out var fresh));
                using (fresh)
                {
                    Assert.IsFalse(fresh.HasFooter);
                    Assert.AreEqual(6, fresh.ChunkCount);
                    store.Preload(fresh);
                    var rng = new Random(5);
                    for (int n = 0; n < 100; n++)
                    {
                        long tick = rng.Next(6 * ChunkTicks);
                        Assert.IsTrue(store.TryGetFrame(tick, out var frame), "tick " + tick);
                        FrameTestWorld.AssertFrameEquals(records[(int)tick], frame, "disk-backed");
                        Assert.IsTrue(store.MemoryChunkCount <= 3, "at most budget + the chunk being read");
                    }
                }
            }
            finally
            {
                Cleanup(path);
            }
        }
    }
}
