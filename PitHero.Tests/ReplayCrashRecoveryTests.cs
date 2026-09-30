using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PitHero.Services;
using PitHero.Services.Replay;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// Launch-time recovery of a session that never reached the quit path (issue #444): the autosave's
    /// recovery recording is promoted to the hero's auto replay and the footer-less session frame file
    /// is finished with the recording's console lines and moved beside it as the cache; the tiebreak
    /// against an existing auto replay; the fallbacks when the frames are short, long, missing or foreign.
    /// </summary>
    [TestClass]
    public class ReplayCrashRecoveryTests
    {
        private const int ChunkTicks = GameConfig.ReplayFrameChunkTicks;
        private const int HeroId = 0x2A;
        private const string RecoveryName = "replay_recovery_0000002A.bin";
        private const string AutoName = "replay_auto_0000002A.bin";

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_crashrecovery_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static ReplayData BuildRecording(int seed, long recordedAtUtcTicks, long totalTicks, int commandEveryTicks = 50)
        {
            var data = new ReplayData
            {
                Kind = ReplayKind.Load,
                MasterSeed = seed,
                HeroName = "Ann",
                JobName = "Knight",
                PitLevelAtStart = 3,
                HeroId = HeroId,
                RecordedAtUtcTicks = recordedAtUtcTicks,
                TotalTicks = totalTicks,
                BuildId = "test",
                SimulationVersion = GameConfig.SimulationVersion,
                StateBlob = new byte[] { 1, 2, 3 },
            };
            for (long t = 0; t < totalTicks; t += commandEveryTicks)
                data.Commands.Add(new ReplayCommandRecord(t, new PlayerCommand(PlayerCommandType.SetManualPause) { A = (int)t }));
            for (long t = 0; t < totalTicks; t += GameConfig.ReplayHashIntervalTicks)
                data.StateHashes.Add(new ReplayHashSample(t, (ulong)(seed * 31 + t)));
            data.ConsoleLog = new RecordedConsoleLog();
            for (long t = 0; t < totalTicks; t += 90)
                data.ConsoleLog.Add(t, new[] { new ConsoleSegment("line at " + t, Color.White) });
            return data;
        }

        /// <summary>A session file as a crash leaves it: header + <paramref name="chunkCount"/> chunks, no footer, optionally a partial trailing chunk.</summary>
        private static string WriteSessionFile(ReplayFileService svc, ReplayData data, int chunkCount, bool partialTail = false, bool footer = false, int seedOffset = 0)
        {
            var identity = new FrameSidecarIdentity(data.MasterSeed + seedOffset, data.RecordedAtUtcTicks, data.SimulationVersion, GameConfig.ReplayFrameFormatVersion, -1);
            var chunks = FrameTestWorld.EncodeChunks(FrameTestWorld.Small(data.MasterSeed), ChunkTicks, chunkCount, new List<FrameTestWorld.TickRecord>());
            string path = svc.SessionFilePath(data.RecordedAtUtcTicks);
            var writer = FrameSidecarWriter.Create(path, identity, ChunkTicks);
            for (int i = 0; i < chunks.Count; i++)
                writer.Append(chunks[i]);
            if (footer)
            {
                var stale = new RecordedConsoleLog();
                stale.Add(0, new[] { new ConsoleSegment("stale footer line", Color.Red) });
                writer.Finish((long)chunkCount * ChunkTicks, stale);
            }
            else
            {
                writer.Dispose();
                if (partialTail)
                {
                    // A chunk the crash cut in half: a plausible length prefix and too few bytes
                    using (var fs = new FileStream(path, FileMode.Append))
                    {
                        fs.Write(new byte[] { 0x00, 0x10, 0x00, 0x00, 1, 2, 3, 4, 5, 6, 7 }, 0, 11);
                    }
                }
            }
            return path;
        }

        private static void AssertCache(ReplayFileService svc, string fileName, ReplayData expected, int chunkCount, int consoleLines)
        {
            Assert.IsTrue(svc.HasValidFrameCache(fileName, expected), "valid cache beside " + fileName);
            Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, FrameSidecarReader.Open(svc.FrameCachePath(fileName), out var reader));
            using (reader)
            {
                Assert.IsTrue(reader.HasFooter);
                Assert.AreEqual(expected.TotalTicks, reader.TotalTicks);
                Assert.AreEqual(chunkCount, reader.ChunkCount);
                Assert.IsTrue(reader.HasConsoleLog, "the footer carries the recording's console lines");
                var log = new RecordedConsoleLog();
                reader.ReadConsoleLog(log);
                Assert.AreEqual(consoleLines, log.Count);
            }
        }

        [TestMethod]
        public void Run_PromotesTheRecovery_AndFinishesTheCrashedSessionFileAsItsCache()
        {
            var dir = NewTempDir();
            try
            {
                var svc = new ReplayFileService(dir);
                long when = new DateTime(2026, 9, 30, 1, 0, 0, DateTimeKind.Utc).Ticks;
                var data = BuildRecording(700, when, 3 * ChunkTicks);
                svc.WriteRecovery(data);
                string sessionPath = WriteSessionFile(svc, data, 3, partialTail: true);

                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));

                Assert.IsFalse(File.Exists(Path.Combine(dir, RecoveryName)), "the recovery file is gone");
                Assert.IsFalse(File.Exists(sessionPath), "the session file moved beside the recording");
                Assert.AreEqual(0, svc.ProtectedSessionFilePaths().Count);
                var list = svc.Enumerate();
                Assert.AreEqual(1, list.Count);
                Assert.AreEqual(AutoName, list[0].FileName);
                Assert.IsTrue(list[0].IsAutoSave);
                Assert.IsTrue(list[0].HasFrameCache, "listed as Cached");
                var promoted = svc.Load(AutoName);
                Assert.AreEqual(3 * ChunkTicks, promoted.TotalTicks, "nothing trimmed: the frames reach the recording's end");
                Assert.AreEqual(data.Commands.Count, promoted.Commands.Count);
                AssertCache(svc, AutoName, promoted, 3, data.ConsoleLog.Count);

                // A second launch finds nothing to do
                Assert.AreEqual(0, ReplayCrashRecovery.Run(svc));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Run_TrimsTheRecordingToTheFramesOnDisk_AndCutsChunksPastItsEnd()
        {
            var dir = NewTempDir();
            try
            {
                // Frames stop one partial chunk short of the recording (the crash swallowed the builder)
                var svc = new ReplayFileService(dir);
                long when = new DateTime(2026, 9, 30, 2, 0, 0, DateTimeKind.Utc).Ticks;
                var shortFrames = BuildRecording(701, when, 3 * ChunkTicks + 45);
                svc.WriteRecovery(shortFrames);
                WriteSessionFile(svc, shortFrames, 3);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                var trimmed = svc.Load(AutoName);
                Assert.AreEqual(3 * ChunkTicks, trimmed.TotalTicks, "ends where the frames end");
                int expectedCommands = 0;
                for (int i = 0; i < shortFrames.Commands.Count; i++)
                    if (shortFrames.Commands[i].Tick < 3 * ChunkTicks) expectedCommands++;
                Assert.AreEqual(expectedCommands, trimmed.Commands.Count, "commands after the new end are dropped");
                int expectedLines = shortFrames.ConsoleLog.CountAtOrBefore(3 * ChunkTicks - 1);
                Assert.AreEqual(expectedLines, trimmed.ConsoleLog.Count);
                AssertCache(svc, AutoName, trimmed, 3, expectedLines);
                svc.Delete(AutoName);

                // Frames run past the recording (chunks appended after the last autosave): the extra chunks are cut
                var longFrames = BuildRecording(702, when + 1, ChunkTicks + 10);
                svc.WriteRecovery(longFrames);
                WriteSessionFile(svc, longFrames, 4);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                var kept = svc.Load(AutoName);
                Assert.AreEqual(ChunkTicks + 10, kept.TotalTicks, "not trimmed");
                AssertCache(svc, AutoName, kept, 2, longFrames.ConsoleLog.Count);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Run_Tiebreak_AgainstAnExistingAutoReplay()
        {
            var dir = NewTempDir();
            try
            {
                var svc = new ReplayFileService(dir);
                long when = new DateTime(2026, 9, 30, 3, 0, 0, DateTimeKind.Utc).Ticks;

                // Different session: the recovery wins and the previous auto replay's cache goes with it
                var oldAuto = BuildRecording(800, when - 1000, 2 * ChunkTicks);
                svc.SaveAuto(oldAuto);
                string oldCache = svc.FrameCachePath(AutoName);
                File.WriteAllBytes(oldCache, new byte[] { 1, 2, 3 });
                var crashed = BuildRecording(801, when, ChunkTicks);
                svc.WriteRecovery(crashed);
                WriteSessionFile(svc, crashed, 1);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                Assert.AreEqual(801, svc.Load(AutoName).MasterSeed, "the crashed session replaced the previous auto replay");
                AssertCache(svc, AutoName, svc.Load(AutoName), 1, crashed.ConsoleLog.Count);
                Assert.IsFalse(File.Exists(Path.Combine(dir, RecoveryName)));

                // Same session, auto longer and the recovery its prefix (a crash during the quit-time save): the auto stays
                var full = BuildRecording(802, when + 1, 4 * ChunkTicks);
                svc.SaveAuto(full);
                var prefix = BuildRecording(802, when + 1, 2 * ChunkTicks);
                svc.WriteRecovery(prefix);
                string prefixSession = WriteSessionFile(svc, prefix, 2);
                Assert.AreEqual(0, ReplayCrashRecovery.Run(svc));
                Assert.AreEqual(4 * ChunkTicks, svc.Load(AutoName).TotalTicks, "the complete auto replay is kept");
                Assert.IsFalse(File.Exists(Path.Combine(dir, RecoveryName)), "the superseded recovery is deleted");
                Assert.IsFalse(svc.Enumerate()[0].HasFrameCache, "a prefix session file is no cache for the longer auto replay");
                Assert.IsTrue(File.Exists(prefixSession), "an unmatched session file is left for the stale cleanup");
                File.Delete(prefixSession);

                // Same session, auto shorter: the recovery is the longer prefix and wins
                var longer = BuildRecording(802, when + 1, 6 * ChunkTicks);
                svc.WriteRecovery(longer);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                Assert.AreEqual(6 * ChunkTicks, svc.Load(AutoName).TotalTicks);

                // Same session but a Time Travel branch: shorter, yet not a prefix, so the branch (the player's real timeline) wins
                var branch = BuildRecording(802, when + 1, 3 * ChunkTicks);
                var record = branch.Commands[2];
                branch.Commands[2] = new ReplayCommandRecord(record.Tick, new PlayerCommand(PlayerCommandType.SetManualPause) { A = 999 });
                svc.WriteRecovery(branch);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                var promoted = svc.Load(AutoName);
                Assert.AreEqual(3 * ChunkTicks, promoted.TotalTicks);
                Assert.AreEqual(999, promoted.Commands[2].Command.A);

                Assert.IsTrue(ReplayCrashRecovery.RecoveryWins(branch, longer));
                Assert.IsFalse(ReplayCrashRecovery.RecoveryWins(prefix, full));
                Assert.IsTrue(ReplayCrashRecovery.RecoveryWins(longer, full));
                Assert.IsTrue(ReplayCrashRecovery.RecoveryWins(crashed, oldAuto));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Run_WithoutUsableFrames_PromotesUncached_AndDropsAnUnreadableRecovery()
        {
            var dir = NewTempDir();
            try
            {
                var svc = new ReplayFileService(dir);
                long when = new DateTime(2026, 9, 30, 4, 0, 0, DateTimeKind.Utc).Ticks;

                // No session file at all
                var none = BuildRecording(900, when, 2 * ChunkTicks);
                svc.WriteRecovery(none);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                var list = svc.Enumerate();
                Assert.AreEqual(1, list.Count);
                Assert.IsTrue(list[0].IsAutoSave);
                Assert.IsFalse(list[0].HasFrameCache, "plays by re-simulation");
                svc.Delete(AutoName);

                // A session file of another recording (seed mismatch): promoted uncached, the file left alone
                var foreign = BuildRecording(901, when + 1, 2 * ChunkTicks);
                svc.WriteRecovery(foreign);
                string foreignSession = WriteSessionFile(svc, foreign, 2, seedOffset: 5);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                Assert.IsFalse(svc.Enumerate()[0].HasFrameCache);
                Assert.IsTrue(File.Exists(foreignSession));
                File.Delete(foreignSession);
                svc.Delete(AutoName);

                // An empty session file (header only): promoted uncached
                var empty = BuildRecording(902, when + 2, ChunkTicks);
                svc.WriteRecovery(empty);
                WriteSessionFile(svc, empty, 0);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                Assert.IsFalse(svc.Enumerate()[0].HasFrameCache);
                svc.Delete(AutoName);

                // A footered session file (the session ended by an in-game Load): re-finished with the recording's lines
                var footered = BuildRecording(903, when + 3, 2 * ChunkTicks);
                svc.WriteRecovery(footered);
                WriteSessionFile(svc, footered, 2, footer: true);
                Assert.AreEqual(1, ReplayCrashRecovery.Run(svc));
                AssertCache(svc, AutoName, svc.Load(AutoName), 2, footered.ConsoleLog.Count);
                svc.Delete(AutoName);

                // Garbage where the recovery file should be: deleted so it stops protecting a session file
                File.WriteAllBytes(Path.Combine(dir, RecoveryName), new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });
                Assert.AreEqual(0, ReplayCrashRecovery.Run(svc));
                Assert.IsFalse(File.Exists(Path.Combine(dir, RecoveryName)));
                Assert.AreEqual(0, svc.Enumerate().Count);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
