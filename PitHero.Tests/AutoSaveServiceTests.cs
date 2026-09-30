using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Services;
using PitHero.Services.Replay;
using System;
using System.Threading;

namespace PitHero.Tests
{
    /// <summary>Tests for the wall-clock autosave countdown and its main-thread/worker split (issue #409).</summary>
    [TestClass]
    public class AutoSaveServiceTests
    {
        private const float Interval = 30f;

        private static SaveData NewSnapshot()
        {
            return new SaveData { HeroName = "AutoHero" };
        }

        /// <summary>Ticks in fixed 1-second steps until the requested wall time has elapsed.</summary>
        private static void TickSeconds(AutoSaveService service, float seconds, bool canSave = true)
        {
            for (int i = 0; i < (int)seconds; i++)
                service.Tick(1f, canSave);
        }

        [TestMethod]
        public void AutoSave_FiresAtInterval_NotBefore()
        {
            int writes = 0;
            var service = new AutoSaveService(NewSnapshot, _ => Interlocked.Increment(ref writes), null, Interval);

            TickSeconds(service, Interval - 1f);
            service.WaitForCompletion();
            Assert.AreEqual(0, writes, "must not fire before the interval elapses");
            Assert.AreEqual(1f, service.SecondsUntilNextSave, 0.001f);

            service.Tick(1f, true);
            service.WaitForCompletion();
            Assert.AreEqual(1, writes, "fires once the interval has elapsed");
            Assert.AreEqual(Interval, service.SecondsUntilNextSave, 0.001f, "countdown restarts after a save");
        }

        [TestMethod]
        public void AutoSave_Blocked_ResetsCountdownAndNeverFires()
        {
            int writes = 0;
            var service = new AutoSaveService(NewSnapshot, _ => Interlocked.Increment(ref writes), null, Interval);

            TickSeconds(service, Interval - 1f);
            service.Tick(1f, canSave: false);          // blocked exactly when it would have fired
            TickSeconds(service, Interval * 3f, canSave: false);
            service.WaitForCompletion();
            Assert.AreEqual(0, writes, "never fires while blocked");

            // The first allowed tick must NOT fire — the countdown starts over
            service.Tick(1f, true);
            service.WaitForCompletion();
            Assert.AreEqual(0, writes, "a blocked stretch restarts the countdown");
            Assert.AreEqual(Interval - 1f, service.SecondsUntilNextSave, 0.001f);
        }

        [TestMethod]
        public void AutoSave_ResetTimer_RestartsCountdown()
        {
            int writes = 0;
            var service = new AutoSaveService(NewSnapshot, _ => Interlocked.Increment(ref writes), null, Interval);

            TickSeconds(service, Interval - 1f);
            service.ResetTimer();
            TickSeconds(service, Interval - 1f);
            service.WaitForCompletion();
            Assert.AreEqual(0, writes);

            service.Tick(1f, true);
            service.WaitForCompletion();
            Assert.AreEqual(1, writes);
        }

        [TestMethod]
        public void AutoSave_InFlight_DoesNotStartASecondWrite()
        {
            var started = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            int writes = 0;
            var service = new AutoSaveService(NewSnapshot, _ =>
            {
                Interlocked.Increment(ref writes);
                started.Set();
                release.Wait(5000);
            }, null, Interval);

            TickSeconds(service, Interval);
            Assert.IsTrue(service.IsSaving, "write is in flight");
            Assert.IsTrue(started.Wait(5000), "worker started");

            // Several more intervals elapse while the worker is still busy
            TickSeconds(service, Interval * 3f);
            Assert.IsTrue(service.IsSaving);
            Assert.AreEqual(1, writes, "no overlapping write may start");
            Assert.IsFalse(service.TryStartAutoSave(), "explicit start is refused while in flight");

            release.Set();
            service.WaitForCompletion();
            Assert.IsFalse(service.IsSaving);
            Assert.AreEqual(1, writes);
        }

        [TestMethod]
        public void AutoSave_Completion_PublishesSnapshotOnPollingThread()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            int gatherThreadId = -1;
            int writeThreadId = -1;
            int savedThreadId = -1;
            SaveData published = null;

            var service = new AutoSaveService(
                () => { gatherThreadId = Thread.CurrentThread.ManagedThreadId; return NewSnapshot(); },
                _ => { writeThreadId = Thread.CurrentThread.ManagedThreadId; },
                data => { savedThreadId = Thread.CurrentThread.ManagedThreadId; published = data; },
                Interval);

            Assert.IsTrue(service.TryStartAutoSave());
            service.WaitForCompletion();

            Assert.AreEqual(mainThreadId, gatherThreadId, "gather runs on the calling thread");
            Assert.AreEqual(mainThreadId, savedThreadId, "completion callback runs on the polling thread");
            Assert.AreNotEqual(mainThreadId, writeThreadId, "write runs on a worker thread");
            Assert.IsNotNull(published);
            Assert.AreEqual("AutoHero", published.HeroName);
            Assert.IsFalse(service.IsSaving);
        }

        [TestMethod]
        public void AutoSave_FaultingWrite_ReportsFailureAndClearsInFlight()
        {
            Exception reported = null;
            bool published = false;
            var service = new AutoSaveService(
                NewSnapshot,
                _ => throw new InvalidOperationException("disk full"),
                _ => published = true,
                Interval);
            service.Failed += ex => reported = ex;

            Assert.IsTrue(service.TryStartAutoSave());
            service.WaitForCompletion();

            Assert.IsFalse(service.IsSaving, "a failed write must not leave the service stuck in flight");
            Assert.IsNotNull(reported);
            Assert.AreEqual("disk full", reported.Message);
            Assert.IsFalse(published, "a failed write publishes nothing");

            // The service recovers: the next save works
            var ok = new AutoSaveService(NewSnapshot, _ => { }, _ => published = true, Interval);
            Assert.IsTrue(ok.TryStartAutoSave());
            ok.WaitForCompletion();
            Assert.IsTrue(published);
        }

        [TestMethod]
        public void AutoSave_NullSnapshot_DoesNotStart()
        {
            int writes = 0;
            var service = new AutoSaveService(() => null, _ => Interlocked.Increment(ref writes), null, Interval);

            Assert.IsFalse(service.TryStartAutoSave());
            service.WaitForCompletion();
            Assert.AreEqual(0, writes);
            Assert.IsFalse(service.IsSaving);
        }

        // ───────────── the replay stage (issue #444) ─────────────

        private static ReplayData NewReplaySnapshot()
        {
            return new ReplayData { HeroId = 7, TotalTicks = 1800 };
        }

        [TestMethod]
        public void AutoSave_ReplayStage_RunsOnTheWorkerAfterTheGameWrite()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            int gatherReplayThreadId = -1, writeReplayThreadId = -1;
            int order = 0, gameWriteOrder = -1, replayWriteOrder = -1;
            ReplayData written = null;
            bool published = false;

            var service = new AutoSaveService(
                NewSnapshot,
                _ => gameWriteOrder = Interlocked.Increment(ref order),
                _ => published = true,
                Interval);
            service.SetReplayStage(
                () => { gatherReplayThreadId = Thread.CurrentThread.ManagedThreadId; return NewReplaySnapshot(); },
                data => { writeReplayThreadId = Thread.CurrentThread.ManagedThreadId; replayWriteOrder = Interlocked.Increment(ref order); written = data; });

            Assert.IsTrue(service.TryStartAutoSave());
            service.WaitForCompletion();

            Assert.AreEqual(mainThreadId, gatherReplayThreadId, "the replay gather runs on the calling thread");
            Assert.AreNotEqual(mainThreadId, writeReplayThreadId, "the replay write runs on the worker");
            Assert.AreEqual(1, gameWriteOrder, "the game save is written first");
            Assert.AreEqual(2, replayWriteOrder, "the replay is written after it, in the same task");
            Assert.IsNotNull(written);
            Assert.AreEqual(7, written.HeroId);
            Assert.IsTrue(published);
            Assert.IsFalse(service.IsSaving);
        }

        [TestMethod]
        public void AutoSave_ReplayStage_ItsFailureLeavesTheGameSaveIntact()
        {
            Exception reported = null;
            bool published = false;
            int replayWrites = 0;
            var service = new AutoSaveService(NewSnapshot, _ => { }, _ => published = true, Interval);
            service.SetReplayStage(NewReplaySnapshot, _ => { Interlocked.Increment(ref replayWrites); throw new InvalidOperationException("replay disk full"); });
            service.Failed += ex => reported = ex;

            Assert.IsTrue(service.TryStartAutoSave());
            service.WaitForCompletion();

            Assert.AreEqual(1, replayWrites);
            Assert.IsTrue(published, "the game snapshot is published although the replay write threw");
            Assert.IsNull(reported, "a replay failure never counts as an autosave failure");
            Assert.IsFalse(service.IsSaving);

            // And the other way round: a failed game write still lets the replay stage run
            replayWrites = 0;
            var failing = new AutoSaveService(NewSnapshot, _ => throw new InvalidOperationException("disk full"), _ => { }, Interval);
            failing.SetReplayStage(NewReplaySnapshot, _ => Interlocked.Increment(ref replayWrites));
            failing.Failed += ex => reported = ex;
            Assert.IsTrue(failing.TryStartAutoSave());
            failing.WaitForCompletion();
            Assert.AreEqual(1, replayWrites, "the replay stage runs after a failed game write");
            Assert.IsNotNull(reported);
            Assert.AreEqual("disk full", reported.Message);
        }

        [TestMethod]
        public void AutoSave_ReplayStage_SkippedBySaveNowAndByANullGather()
        {
            int gameWrites = 0, replayWrites = 0;
            var service = new AutoSaveService(NewSnapshot, _ => Interlocked.Increment(ref gameWrites), null, Interval);
            service.SetReplayStage(NewReplaySnapshot, _ => Interlocked.Increment(ref replayWrites));

            Assert.IsTrue(service.SaveNow(), "the quit-time save writes the game snapshot");
            Assert.AreEqual(1, gameWrites);
            Assert.AreEqual(0, replayWrites, "the quit path writes the auto replay itself, so SaveNow skips the stage");

            TickSeconds(service, Interval);
            service.WaitForCompletion();
            Assert.AreEqual(2, gameWrites);
            Assert.AreEqual(1, replayWrites, "the periodic save includes the stage");

            var nothing = new AutoSaveService(NewSnapshot, _ => Interlocked.Increment(ref gameWrites), null, Interval);
            nothing.SetReplayStage(() => null, _ => Interlocked.Increment(ref replayWrites));
            Assert.IsTrue(nothing.TryStartAutoSave());
            nothing.WaitForCompletion();
            Assert.AreEqual(3, gameWrites);
            Assert.AreEqual(1, replayWrites, "a null replay gather writes the game save only");
        }

        [TestMethod]
        public void AutoSave_IntervalFromGameConfig_IsThirtySeconds()
        {
            Assert.AreEqual(30f, GameConfig.AutoSaveIntervalSeconds, 0.001f);
            Assert.AreEqual("autosave_", GameConfig.AutoSaveFilePrefix);
            Assert.AreEqual(".bin", GameConfig.AutoSaveFileExtension);
            Assert.AreEqual("autosave.bin", GameConfig.AutoSaveLegacyFileName);
        }
    }
}
