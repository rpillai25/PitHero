using Nez;

namespace PitHero.Tests
{
    /// <summary>
    /// Pins the fixed-step accumulator math that converts wall-clock frame time into simulation steps.
    /// The replay system relies on the step count (not the frame rate) being the only thing that
    /// advances the simulation.
    /// </summary>
    [TestClass]
    public class FixedStepSchedulerTests
    {
        private const float Step = 1f / 60f;

        /// <summary>One 60 Hz frame at 1x yields exactly one step and leaves almost nothing behind.</summary>
        [TestMethod]
        public void ComputeSteps_SingleFrameAtNormalSpeed_YieldsOneStep()
        {
            float acc = 0f;
            int steps = FixedStepScheduler.ComputeSteps(ref acc, Step, 1f, Step, 6);
            Assert.AreEqual(1, steps);
            Assert.IsTrue(acc < 0.0001f, "leftover should be ~0, was " + acc);
        }

        /// <summary>Ten 60 Hz frames at 2.5x speed sum to 25 steps with 2/3 alternation.</summary>
        [TestMethod]
        public void ComputeSteps_FastForward_SumsToSpeedTimesFrames()
        {
            float acc = 0f;
            int total = 0;
            for (int i = 0; i < 10; i++)
            {
                int steps = FixedStepScheduler.ComputeSteps(ref acc, Step, 2.5f, Step, 6);
                Assert.IsTrue(steps == 2 || steps == 3, "expected 2 or 3 steps, got " + steps);
                total += steps;
            }
            // float accumulation may carry one partial step past the window: 24 or 25 both mean 2.5x
            Assert.IsTrue(total == 24 || total == 25, "ten frames at 2.5x should give 24-25 steps, gave " + total);
        }

        /// <summary>A long hitch is capped at MaxStepsPerFrame and the backlog is dropped to one step.</summary>
        [TestMethod]
        public void ComputeSteps_Hitch_CapsAndDropsBacklog()
        {
            float acc = 0f;
            int steps = FixedStepScheduler.ComputeSteps(ref acc, 0.5f, 1f, Step, 6);
            Assert.AreEqual(6, steps);
            Assert.IsTrue(acc <= Step + 0.0001f, "backlog should be clamped to one step, was " + acc);

            // the next normal frame must not explode into a catch-up burst
            int next = FixedStepScheduler.ComputeSteps(ref acc, Step, 1f, Step, 6);
            Assert.IsTrue(next <= 2, "post-hitch frame should run at most 2 steps, ran " + next);
        }

        /// <summary>Speed zero (suspended simulation) runs nothing and leaves the accumulator untouched.</summary>
        [TestMethod]
        public void ComputeSteps_ZeroSpeed_RunsNothing()
        {
            float acc = 0.01f;
            int steps = FixedStepScheduler.ComputeSteps(ref acc, Step, 0f, Step, 6);
            Assert.AreEqual(0, steps);
            Assert.AreEqual(0.01f, acc);
        }

        /// <summary>Sub-step frames (e.g. 144 Hz) accumulate into a step every few frames.</summary>
        [TestMethod]
        public void ComputeSteps_HighRefresh_AccumulatesAcrossFrames()
        {
            float acc = 0f;
            float frame = 1f / 144f;
            int total = 0;
            for (int i = 0; i < 144; i++)
                total += FixedStepScheduler.ComputeSteps(ref acc, frame, 1f, Step, 6);
            Assert.IsTrue(total == 59 || total == 60, "one second at 144 Hz should give ~60 steps, gave " + total);
        }

        /// <summary>Negative wall time (clock glitch) is treated as zero.</summary>
        [TestMethod]
        public void ComputeSteps_NegativeWallTime_IsIgnored()
        {
            float acc = 0f;
            int steps = FixedStepScheduler.ComputeSteps(ref acc, -0.1f, 1f, Step, 6);
            Assert.AreEqual(0, steps);
            Assert.AreEqual(0f, acc);
        }
    }
}
