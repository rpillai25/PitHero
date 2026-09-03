using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>Pins the replay scrubber's time display.</summary>
    [TestClass]
    public class ReplayTimeFormatterTests
    {
        [TestMethod]
        public void FormatSeconds_UnderAnHour_IsMinutesSeconds()
        {
            Assert.AreEqual("00:00", ReplayTimeFormatter.FormatSeconds(0f));
            Assert.AreEqual("00:07", ReplayTimeFormatter.FormatSeconds(7.9f));
            Assert.AreEqual("59:59", ReplayTimeFormatter.FormatSeconds(3599f));
            Assert.AreEqual("00:00", ReplayTimeFormatter.FormatSeconds(-5f));
        }

        [TestMethod]
        public void FormatSeconds_AnHourOrMore_AddsHours()
        {
            Assert.AreEqual("1:00:00", ReplayTimeFormatter.FormatSeconds(3600f));
            Assert.AreEqual("2:05:09", ReplayTimeFormatter.FormatSeconds(2 * 3600 + 5 * 60 + 9));
        }

        [TestMethod]
        public void FormatTicks_UsesFixedStep()
        {
            long ticksPerMinute = 60L * (long)System.Math.Round(1f / GameConfig.SimulationFixedStepSeconds);
            Assert.AreEqual("01:00", ReplayTimeFormatter.FormatTicks(ticksPerMinute));
        }
    }
}
