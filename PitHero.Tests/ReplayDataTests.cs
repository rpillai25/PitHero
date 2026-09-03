using System;
using System.IO;
using Nez.Persistence.Binary;
using PitHero.Services;
using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>
    /// Pins the replay file format: header, commands (all payload fields), tripwire samples, the
    /// embedded SaveData blob, header-only reads and version gating; plus the file service's
    /// naming/listing and the state hasher's sensitivity to the RNG stream.
    /// </summary>
    [TestClass]
    public class ReplayDataTests
    {
        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_replay_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static ReplayData BuildSample(int commandCount)
        {
            var save = new SaveData { HeroName = "Blob Hero", Level = 7, Funds = 1234 };
            var data = new ReplayData
            {
                Kind = ReplayKind.Load,
                MasterSeed = -987654321,
                HeroName = "Sir O'Malley",
                JobName = "Knight",
                PitLevelAtStart = 12,
                RecordedAtUtcTicks = new DateTime(2026, 9, 3, 10, 30, 0, DateTimeKind.Utc).Ticks,
                TotalTicks = 123456789012L,
                BuildId = "1.2.3.4",
                StateBlob = ReplayIO.SerializeSaveData(save),
            };
            for (int i = 0; i < commandCount; i++)
            {
                var cmd = new PlayerCommand((PlayerCommandType)(10 + (i % 5)), i, -i, i * 3, int.MaxValue - i);
                cmd.L = (i % 2 == 0) ? long.MaxValue - i : long.MinValue + i;
                cmd.F = i * 0.25f;
                cmd.S = (i % 3 == 0) ? null : "name" + i;
                data.Commands.Add(new ReplayCommandRecord(i * 7L, in cmd));
            }
            data.Decisions.Add(new ReplayHashSample(5, 0xDEADBEEFCAFEBABEUL));
            data.Decisions.Add(new ReplayHashSample(9, ulong.MaxValue));
            data.StateHashes.Add(new ReplayHashSample(60, 42));
            return data;
        }

        /// <summary>Every field survives a disk round-trip through FileDataStore.</summary>
        [TestMethod]
        public void ReplayData_PersistAndRecover_RoundTrip()
        {
            var dir = NewTempDir();
            try
            {
                var store = new FileDataStore(dir);
                var original = BuildSample(500);
                store.Save("r.bin", original);

                var loaded = new ReplayData();
                store.Load("r.bin", loaded);

                Assert.AreEqual(ReplayData.CurrentVersion, loaded.FormatVersion);
                Assert.AreEqual(original.Kind, loaded.Kind);
                Assert.AreEqual(original.MasterSeed, loaded.MasterSeed);
                Assert.AreEqual(original.HeroName, loaded.HeroName);
                Assert.AreEqual(original.JobName, loaded.JobName);
                Assert.AreEqual(original.PitLevelAtStart, loaded.PitLevelAtStart);
                Assert.AreEqual(original.RecordedAtUtcTicks, loaded.RecordedAtUtcTicks);
                Assert.AreEqual(original.TotalTicks, loaded.TotalTicks);
                Assert.AreEqual(original.BuildId, loaded.BuildId);
                CollectionAssert.AreEqual(original.StateBlob, loaded.StateBlob);

                Assert.AreEqual(original.Commands.Count, loaded.Commands.Count);
                for (int i = 0; i < original.Commands.Count; i++)
                {
                    var a = original.Commands[i];
                    var b = loaded.Commands[i];
                    Assert.AreEqual(a.Tick, b.Tick);
                    Assert.AreEqual(a.Command.Type, b.Command.Type);
                    Assert.AreEqual(a.Command.A, b.Command.A);
                    Assert.AreEqual(a.Command.B, b.Command.B);
                    Assert.AreEqual(a.Command.C, b.Command.C);
                    Assert.AreEqual(a.Command.D, b.Command.D);
                    Assert.AreEqual(a.Command.L, b.Command.L);
                    Assert.AreEqual(a.Command.F, b.Command.F);
                    Assert.AreEqual(a.Command.S, b.Command.S);
                }

                Assert.AreEqual(2, loaded.Decisions.Count);
                Assert.AreEqual(0xDEADBEEFCAFEBABEUL, loaded.Decisions[0].Hash);
                Assert.AreEqual(ulong.MaxValue, loaded.Decisions[1].Hash);
                Assert.AreEqual(1, loaded.StateHashes.Count);
                Assert.AreEqual(60L, loaded.StateHashes[0].Tick);

                var blobSave = ReplayIO.DeserializeSaveData(loaded.StateBlob);
                Assert.AreEqual("Blob Hero", blobSave.HeroName);
                Assert.AreEqual(7, blobSave.Level);
                Assert.AreEqual(1234, blobSave.Funds);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>HeaderOnly reads stop after the header and leave the lists empty.</summary>
        [TestMethod]
        public void ReplayData_HeaderOnly_SkipsBody()
        {
            var dir = NewTempDir();
            try
            {
                var store = new FileDataStore(dir);
                store.Save("r.bin", BuildSample(20));
                var header = new ReplayData { HeaderOnly = true };
                store.Load("r.bin", header);
                Assert.AreEqual("Sir O'Malley", header.HeroName);
                Assert.AreEqual(123456789012L, header.TotalTicks);
                Assert.IsNull(header.StateBlob);
                Assert.AreEqual(0, header.Commands.Count);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>A file with an unsupported version is rejected cleanly.</summary>
        [TestMethod]
        public void ReplayData_UnsupportedVersion_Throws()
        {
            using (var ms = new MemoryStream())
            {
                var writer = new ReuseableBinaryWriter(ms);
                writer.Write(ReplayData.CurrentVersion + 50);
                writer.Write(0);
                writer.Flush();
                var reader = new ReuseableBinaryReader(new MemoryStream(ms.ToArray()));
                Assert.ThrowsException<InvalidDataException>(() => reader.ReadPersistableInto(new ReplayData()));
            }
        }

        /// <summary>Long/ulong helpers round-trip extremes through the int-only writer.</summary>
        [TestMethod]
        public void ReplayIO_LongHelpers_RoundTripExtremes()
        {
            long[] longs = { 0, 1, -1, long.MaxValue, long.MinValue, 123456789012345L, -987654321098L };
            ulong[] ulongs = { 0, 1, ulong.MaxValue, 0x8000000000000000UL, 0xDEADBEEFCAFEBABEUL };
            using (var ms = new MemoryStream())
            {
                var writer = new ReuseableBinaryWriter(ms);
                for (int i = 0; i < longs.Length; i++) ReplayIO.WriteLong(writer, longs[i]);
                for (int i = 0; i < ulongs.Length; i++) ReplayIO.WriteULong(writer, ulongs[i]);
                writer.Flush();
                var reader = new ReuseableBinaryReader(new MemoryStream(ms.ToArray()));
                for (int i = 0; i < longs.Length; i++) Assert.AreEqual(longs[i], ReplayIO.ReadLong(reader));
                for (int i = 0; i < ulongs.Length; i++) Assert.AreEqual(ulongs[i], ReplayIO.ReadULong(reader));
            }
        }

        /// <summary>Save, list (newest first), load and delete through the file service; names are sanitized.</summary>
        [TestMethod]
        public void ReplayFileService_SaveEnumerateLoadDelete()
        {
            var dir = NewTempDir();
            try
            {
                var svc = new ReplayFileService(dir);
                Assert.AreEqual(0, svc.Enumerate().Count);

                var older = BuildSample(3);
                older.RecordedAtUtcTicks = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
                var newer = BuildSample(4);
                newer.HeroName = "Zed";
                newer.RecordedAtUtcTicks = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

                string f1 = svc.Save(older);
                string f2 = svc.Save(newer);
                Assert.IsTrue(f1.StartsWith("replay_Sir_OMalley_"), f1);
                Assert.IsTrue(f2.StartsWith("replay_Zed_"), f2);
                Assert.AreNotEqual(f1, f2);

                var list = svc.Enumerate();
                Assert.AreEqual(2, list.Count);
                Assert.AreEqual("Zed", list[0].HeroName, "newest first");
                Assert.AreEqual(f2, list[0].FileName);
                Assert.AreEqual(12, list[1].PitLevelAtStart);

                var loaded = svc.Load(f1);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(3, loaded.Commands.Count);

                Assert.IsTrue(svc.Delete(f1));
                Assert.IsFalse(svc.Delete(f1));
                Assert.AreEqual(1, svc.Enumerate().Count);
                Assert.IsNull(svc.Load(f1));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>Name sanitization keeps ASCII word characters only and never yields an empty name.</summary>
        [TestMethod]
        public void ReplayFileService_SanitizeName()
        {
            Assert.AreEqual("Sir_OMalley_3", ReplayFileService.SanitizeName("Sir O'Malley <3"));
            Assert.AreEqual("Hero", ReplayFileService.SanitizeName(""));
            Assert.AreEqual("Hero", ReplayFileService.SanitizeName("!!!"));
            Assert.AreEqual(24, ReplayFileService.SanitizeName(new string('a', 40)).Length);
        }

        /// <summary>Two identical recorders snapshot identical data; a recording paused for playback appends nothing.</summary>
        [TestMethod]
        public void ReplayRecorder_SnapshotAndPause()
        {
            var recorder = new ReplayRecorder();
            try
            {
                recorder.Initialize(ReplayKind.NewGame, 5, null, null);
                recorder.SetSessionInfo("Ann", "Mage", 3);
                recorder.RecordCommand(10, new PlayerCommand(PlayerCommandType.Replenish));
                recorder.RecordDecision(11, 99);
                recorder.RecordStateHash(60, 7);
                recorder.IsRecording = false;
                recorder.RecordCommand(12, new PlayerCommand(PlayerCommandType.Replenish));

                var snap = recorder.Snapshot(500);
                Assert.AreEqual(ReplayKind.NewGame, snap.Kind);
                Assert.AreEqual("Ann", snap.HeroName);
                Assert.AreEqual("Mage", snap.JobName);
                Assert.AreEqual(3, snap.PitLevelAtStart);
                Assert.AreEqual(500L, snap.TotalTicks);
                Assert.AreEqual(1, snap.Commands.Count, "paused recorder must not append");
                Assert.AreEqual(1, snap.Decisions.Count);
                Assert.AreEqual(1, snap.StateHashes.Count);

                // Preloading from a snapshot continues the same recording
                var second = new ReplayRecorder();
                second.Initialize(ReplayKind.NewGame, 5, null, snap);
                second.RecordCommand(600, new PlayerCommand(PlayerCommandType.Replenish));
                Assert.AreEqual(2, second.Snapshot(700).Commands.Count);
                Assert.AreEqual(snap.RecordedAtUtcTicks, second.Snapshot(700).RecordedAtUtcTicks);
                second.Detach();
            }
            finally
            {
                recorder.Detach();
            }
        }

        /// <summary>The state hash changes when the simulation RNG advances (headless: RNG + tick only).</summary>
        [TestMethod]
        public void SimulationStateHasher_TracksRngState()
        {
            GameRandom.InitializeSession(31337);
            ulong a = SimulationStateHasher.Compute(60);
            ulong b = SimulationStateHasher.Compute(60);
            Assert.AreEqual(a, b, "same state, same hash");
            Nez.Random.NextFloat();
            ulong c = SimulationStateHasher.Compute(60);
            Assert.AreNotEqual(a, c, "one extra roll must change the hash");
            Assert.AreNotEqual(a, SimulationStateHasher.Compute(61), "tick is part of the hash");
            Nez.Random.SetSeed(Environment.TickCount);
        }

        /// <summary>Plan hashing is order-sensitive and tile-sensitive.</summary>
        [TestMethod]
        public void ReplayTripwire_HashPlan_IsOrderAndTileSensitive()
        {
            var a1 = new Nez.AI.GOAP.Action("Jump");
            var a2 = new Nez.AI.GOAP.Action("Wander");
            var p1 = new System.Collections.Generic.Stack<Nez.AI.GOAP.Action>();
            p1.Push(a2); p1.Push(a1);
            var p2 = new System.Collections.Generic.Stack<Nez.AI.GOAP.Action>();
            p2.Push(a1); p2.Push(a2);
            Assert.AreEqual(ReplayTripwire.HashPlan(p1, 3, 4), ReplayTripwire.HashPlan(p1, 3, 4));
            Assert.AreNotEqual(ReplayTripwire.HashPlan(p1, 3, 4), ReplayTripwire.HashPlan(p2, 3, 4));
            Assert.AreNotEqual(ReplayTripwire.HashPlan(p1, 3, 4), ReplayTripwire.HashPlan(p1, 4, 3));
        }
    }
}
