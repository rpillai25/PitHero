using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Services.AutoJob;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the BackpressureTracker smoother (issue #375): rising pressure is granted
    /// instantly, falling pressure decays through the EMA, and grants drain at most one worker
    /// per drain interval.
    /// </summary>
    [TestClass]
    public class BackpressureTrackerTests
    {
        [TestMethod]
        public void Sample_RisingPressure_GrantsInstantly()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(3f, nowSeconds: 0f);
            Assert.AreEqual(3, tracker.GrantedWorkers, "A pressure spike is granted on the very next sample");
        }

        [TestMethod]
        public void Sample_FractionalPressure_RoundsUp()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(1.4f, nowSeconds: 0f);
            Assert.AreEqual(2, tracker.GrantedWorkers, "Workers are whole monsters — pressure rounds up");
        }

        [TestMethod]
        public void Sample_FallingPressure_DecaysThroughEma()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(4f, 0f);
            tracker.Sample(0f, 5f);
            Assert.IsTrue(tracker.SmoothedPressure > 0f && tracker.SmoothedPressure < 4f,
                "One low sample only decays smoothed pressure part-way");
        }

        [TestMethod]
        public void Sample_NoDrainBeforeInterval()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(2f, 0f);
            float justBefore = GameConfig.AutoJobScaleDownDrainIntervalSeconds - 1f;
            for (float t = 5f; t <= justBefore; t += 5f)
                tracker.Sample(0f, t);
            Assert.AreEqual(2, tracker.GrantedWorkers,
                "Grants must hold until a full drain interval has elapsed");
        }

        [TestMethod]
        public void Sample_DrainsExactlyOneWorkerPerInterval()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(3f, 0f);
            Assert.AreEqual(3, tracker.GrantedWorkers);

            // Hold raw pressure at zero across two full drain intervals: one worker released per
            // interval, never a jump straight to zero.
            float interval = GameConfig.AutoJobScaleDownDrainIntervalSeconds;
            for (float t = 5f; t <= interval * 2f + 5f; t += 5f)
            {
                int before = tracker.GrantedWorkers;
                tracker.Sample(0f, t);
                Assert.IsTrue(before - tracker.GrantedWorkers <= 1,
                    "A single sample may release at most one worker");
            }
            Assert.AreEqual(1, tracker.GrantedWorkers,
                "Two elapsed drain intervals release exactly two workers");
        }

        [TestMethod]
        public void Sample_ReboundDuringDrain_RestoresGrantInstantly()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(3f, 0f);
            float interval = GameConfig.AutoJobScaleDownDrainIntervalSeconds;
            for (float t = 5f; t <= interval + 5f; t += 5f)
                tracker.Sample(0f, t);
            Assert.AreEqual(2, tracker.GrantedWorkers, "One worker drained");

            tracker.Sample(3f, interval + 10f);
            Assert.AreEqual(3, tracker.GrantedWorkers, "The rush came back — regrant instantly");
        }

        [TestMethod]
        public void Reset_ClearsSmoothedPressureAndGrants()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(5f, 100f);
            tracker.Reset(0f);
            Assert.AreEqual(0, tracker.GrantedWorkers);
            Assert.AreEqual(0f, tracker.SmoothedPressure);
        }

        [TestMethod]
        public void Sample_NegativeRawClampsToZero()
        {
            var tracker = new BackpressureTracker();
            tracker.Sample(-2f, 0f);
            Assert.AreEqual(0, tracker.GrantedWorkers);
            Assert.AreEqual(0f, tracker.SmoothedPressure);
        }
    }
}
