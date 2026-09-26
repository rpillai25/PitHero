using System;
using System.Collections.Generic;
using System.IO;
using PitHero.Services;
using PitHero.Services.Replay;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The replay file service's frame caches (issue #429): a listed replay is flagged only when a
    /// finished sidecar with its exact identity and tick count sits beside it; Delete removes both files;
    /// the disk budget deletes the oldest caches and never a recording.
    /// </summary>
    [TestClass]
    public class ReplayFileServiceTests
    {
        private const int ChunkTicks = 60;

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_replayfiles_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static ReplayData BuildReplay(string heroName, int seed, long recordedAtUtcTicks, int chunkCount)
        {
            return new ReplayData
            {
                Kind = ReplayKind.Load,
                MasterSeed = seed,
                HeroName = heroName,
                JobName = "Knight",
                PitLevelAtStart = 1,
                HeroId = 7,
                RecordedAtUtcTicks = recordedAtUtcTicks,
                TotalTicks = (long)chunkCount * ChunkTicks,
                BuildId = "test",
                SimulationVersion = GameConfig.SimulationVersion,
                StateBlob = new byte[] { 1, 2, 3 },
            };
        }

        /// <summary>Writes a finished cache for <paramref name="data"/> beside the saved file, optionally with another identity or tick count.</summary>
        private static string WriteCache(ReplayFileService svc, string fileName, ReplayData data, int chunkCount, int seedOffset = 0, long totalTicksOverride = -1, bool finish = true)
        {
            var identity = new FrameSidecarIdentity(data.MasterSeed + seedOffset, data.RecordedAtUtcTicks, data.SimulationVersion, GameConfig.ReplayFrameFormatVersion, -1);
            var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(data.MasterSeed), ChunkTicks, chunkCount, new List<FrameTestWorld.TickRecord>());
            string path = svc.FrameCachePath(fileName);
            using (var writer = FrameSidecarWriter.Create(path, identity, ChunkTicks))
            {
                for (int i = 0; i < chunks.Count; i++)
                    writer.Append(chunks[i]);
                if (finish)
                    writer.Finish(totalTicksOverride >= 0 ? totalTicksOverride : data.TotalTicks, null);
            }
            return path;
        }

        [TestMethod]
        public void Enumerate_FlagsOnlyAValidFrameCache()
        {
            var dir = NewTempDir();
            try
            {
                var svc = new ReplayFileService(dir);
                long when = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc).Ticks;
                var valid = BuildReplay("Valid", 100, when, 3);
                var none = BuildReplay("NoCache", 101, when + 1, 3);
                var wrongSeed = BuildReplay("WrongSeed", 102, when + 2, 3);
                var wrongTicks = BuildReplay("WrongTicks", 103, when + 3, 3);
                var unfinished = BuildReplay("Unfinished", 104, when + 4, 3);
                var shortStream = BuildReplay("Short", 105, when + 5, 3);

                string fValid = svc.Save(valid);
                string fNone = svc.Save(none);
                string fWrongSeed = svc.Save(wrongSeed);
                string fWrongTicks = svc.Save(wrongTicks);
                string fUnfinished = svc.Save(unfinished);
                string fShort = svc.Save(shortStream);
                WriteCache(svc, fValid, valid, 3);
                WriteCache(svc, fWrongSeed, wrongSeed, 3, seedOffset: 1);
                WriteCache(svc, fWrongTicks, wrongTicks, 3, totalTicksOverride: 3 * ChunkTicks - 5);
                WriteCache(svc, fUnfinished, unfinished, 3, finish: false);
                WriteCache(svc, fShort, shortStream, 2); // footer says 180 ticks, frames stop at 119
                Assert.AreEqual(FrameSidecarFile.OpenResult.Missing, svc.TryOpenFrameCache(fNone, none.MasterSeed, none.RecordedAtUtcTicks, none.SimulationVersion, none.TotalTicks, out var r0));
                Assert.IsNull(r0);
                Assert.AreEqual(FrameSidecarFile.OpenResult.IdentityMismatch, svc.TryOpenFrameCache(fWrongSeed, wrongSeed.MasterSeed, wrongSeed.RecordedAtUtcTicks, wrongSeed.SimulationVersion, wrongSeed.TotalTicks, out _));
                Assert.AreEqual(FrameSidecarFile.OpenResult.IdentityMismatch, svc.TryOpenFrameCache(fWrongTicks, wrongTicks.MasterSeed, wrongTicks.RecordedAtUtcTicks, wrongTicks.SimulationVersion, wrongTicks.TotalTicks, out _));
                Assert.AreEqual(FrameSidecarFile.OpenResult.Incomplete, svc.TryOpenFrameCache(fUnfinished, unfinished.MasterSeed, unfinished.RecordedAtUtcTicks, unfinished.SimulationVersion, unfinished.TotalTicks, out _));
                Assert.AreEqual(FrameSidecarFile.OpenResult.Incomplete, svc.TryOpenFrameCache(fShort, shortStream.MasterSeed, shortStream.RecordedAtUtcTicks, shortStream.SimulationVersion, shortStream.TotalTicks, out _));
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, svc.TryOpenFrameCache(fValid, valid.MasterSeed, valid.RecordedAtUtcTicks, valid.SimulationVersion, valid.TotalTicks, out var reader));
                using (reader)
                {
                    Assert.AreEqual(3, reader.ChunkCount);
                    Assert.AreEqual(valid.TotalTicks, reader.TotalTicks);
                }

                var list = svc.Enumerate();
                Assert.AreEqual(6, list.Count);
                var flags = new Dictionary<string, bool>();
                for (int i = 0; i < list.Count; i++)
                    flags[list[i].HeroName] = list[i].HasFrameCache;
                Assert.IsTrue(flags["Valid"]);
                Assert.IsFalse(flags["NoCache"]);
                Assert.IsFalse(flags["WrongSeed"]);
                Assert.IsFalse(flags["WrongTicks"]);
                Assert.IsFalse(flags["Unfinished"]);
                Assert.IsFalse(flags["Short"]);
                Assert.AreEqual(100, list[5].MasterSeed, "oldest last; the seed rides along in the header");

                // Deleting the cache by hand drops the mark; the recording still lists and loads
                File.Delete(svc.FrameCachePath(fValid));
                list = svc.Enumerate();
                for (int i = 0; i < list.Count; i++)
                    Assert.IsFalse(list[i].HasFrameCache, list[i].HeroName);
                Assert.IsNotNull(svc.Load(fValid));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Delete_RemovesTheRecordingAndItsCache()
        {
            var dir = NewTempDir();
            try
            {
                var svc = new ReplayFileService(dir);
                var data = BuildReplay("Gone", 200, DateTime.UtcNow.Ticks, 2);
                string fileName = svc.Save(data);
                string cache = WriteCache(svc, fileName, data, 2);
                Assert.IsTrue(File.Exists(cache));
                Assert.IsTrue(svc.Delete(fileName));
                Assert.IsFalse(File.Exists(Path.Combine(dir, fileName)));
                Assert.IsFalse(File.Exists(cache), "the cache goes with the recording");
                Assert.IsFalse(svc.Delete(fileName));

                // A cache without a recording is removed too
                File.WriteAllBytes(cache, new byte[] { 1 });
                Assert.IsFalse(svc.Delete(fileName));
                Assert.IsFalse(File.Exists(cache));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void FrameCacheBudget_DeletesTheOldestCachesOnly_NeverARecording()
        {
            var dir = NewTempDir();
            try
            {
                var svc = new ReplayFileService(dir);
                long when = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
                var files = new string[4];
                var caches = new string[4];
                var sizes = new long[4];
                long total = 0;
                for (int i = 0; i < 4; i++)
                {
                    var data = BuildReplay("Hero" + i, 300 + i, when + i, 2);
                    files[i] = svc.Save(data);
                    caches[i] = WriteCache(svc, files[i], data, 2);
                    File.SetLastWriteTimeUtc(caches[i], new DateTime(2026, 9, 1 + i, 12, 0, 0, DateTimeKind.Utc));
                    sizes[i] = new FileInfo(caches[i]).Length;
                    total += sizes[i];
                }
                // The live session's file is not a cache: neither counted nor deleted
                string session = Path.Combine(dir, GameConfig.ReplayFrameSessionFilePrefix + "1.frames");
                File.WriteAllBytes(session, new byte[total]);

                Assert.AreEqual(0, svc.EnforceFrameCacheBudget(total), "within budget: nothing deleted");
                // Room for the two newest plus half of the second oldest: exactly the two oldest must go
                Assert.AreEqual(2, svc.EnforceFrameCacheBudget(sizes[2] + sizes[3] + sizes[1] / 2), "the two oldest go");
                Assert.IsFalse(File.Exists(caches[0]));
                Assert.IsFalse(File.Exists(caches[1]));
                Assert.IsTrue(File.Exists(caches[2]));
                Assert.IsTrue(File.Exists(caches[3]));
                Assert.IsTrue(File.Exists(session));
                for (int i = 0; i < 4; i++)
                    Assert.IsTrue(File.Exists(Path.Combine(dir, files[i])), "recording " + i + " untouched");

                Assert.AreEqual(2, svc.EnforceFrameCacheBudget(0), "a zero budget clears every cache");
                Assert.IsFalse(File.Exists(caches[3]));
                Assert.IsTrue(File.Exists(session));
                Assert.AreEqual(4, svc.Enumerate().Count);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
