using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Services;
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
